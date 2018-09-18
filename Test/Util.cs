using NUnit.Framework;

namespace Es6Util.Test
{
	[TestFixture]
	public class Util
	{
		[Test]
		public void Update_converted_directories()
		{
			Es6Converter.UpdateConvertedDirectory(sourceDir, convertedSourceDir);
			Es6Converter.UpdateConvertedDirectory(testDir, convertedTestDir);
		}

		[Test]
		public void Update_original_directories()
		{
			Es6Converter.UpdateOriginalDirectory(convertedSourceDir, sourceDir);
			Es6Converter.UpdateOriginalDirectory(convertedTestDir, testDir);
		}

		[Test]
		public void Convert()
		{
			string content = @"define([
	'dojo/_base/array',
	'dojo/Deferred',

	'aps/xhr'
], function (
	dojoArray,
	Deferred,

	xhr
) {
	'use strict';

	var SERVICE_URI = '/aps/2/services/order-manager/orders/';

	// aps.id of current user's account
	var contextAccount = null;

	var orderTypes = {
		sales: 'SALES'
	};

	function getContextAccount() {
		if (contextAccount && contextAccount.aps && contextAccount.aps.id === aps.context.user.aps.id) {
			return (new Deferred()).resolve(contextAccount);
		}

		return xhr.get('/aps/2/resources/' + aps.context.user.aps.id + '/organization')
			.then(function (accountInfo) {
				contextAccount = accountInfo[0];
				return contextAccount;
			});
	}


	return {
		// TODO: move to more appropriate place
		getContextAccount: getContextAccount,

		get: function (orderId) {
			return xhr.get(SERVICE_URI + orderId, {
				headers: { 'Content-Type': 'application/json' }
			});
		},

		prepareOrder: function(products, selectedResources) {
			var order = {
				type: orderTypes.sales,
				products: []
			};

			dojoArray.forEach(products, function (product) {
				var orderProduct = {
					planId: product.planId,
					period: {
						unit: product.period.unit,
						duration: product.period.duration
					}
				};
				if (!selectedResources && product.resources) {
					orderProduct.resources = product.resources.map(function (res) {
						return {
							resourceId: res.resourceId,
							amount: res.amount
						};
					});
				}

				order.products.push(orderProduct);
			});

			if (selectedResources) {
				dojoArray.forEach(order.products, function (product) {
					if (selectedResources[product.planId]) {
						product.resources = selectedResources[product.planId].map(function (res) {
							return {
								resourceId: res.resourceId,
								amount: res.amount
							};
						});
					}
				});
			}

			return order;
		},

		place: function (order) {
			return xhr.post(SERVICE_URI, {
				data: JSON.stringify(order),
				headers: { 'Content-Type': 'application/json' }
			});
		},

		estimate: function (order, includeTaxes, getCosts) {
			var getAccount;
			if (getCosts) {
				getAccount = getContextAccount().then(account => account.aps.id);
			} else {
				getAccount = (new Deferred()).resolve(order.accountId);
			}

			var uri = SERVICE_URI + 'estimate' + (includeTaxes ? '' : '?includeTaxes=false');
			return getAccount
				.then(function (accountId) {
					order.accountId = accountId;
					return xhr.post(uri, {
						data: JSON.stringify(order),
						headers: { 'Content-Type': 'application/json' }
					});
				});
		},

		getTermsAndConditions: function(order) {
			var uri = SERVICE_URI + 'termsconditions';
			return xhr.post(uri, {
				data: JSON.stringify(order),
				headers: { 'Content-Type': 'application/json' }
			});
		},

		estimateCosts: function(order, includeTaxes) {
			var uri = SERVICE_URI + 'estimateCosts' + (includeTaxes ? '' : '?includeTaxes=false');
			return xhr.post(uri, {
				data: JSON.stringify(order),
				headers: { 'Content-Type': 'application/json' }
			});
		}
	};
});
";

			string converted = Es6Converter.ConvertToEs6(content);
			Assert.That(converted, Is.Not.Null);
		}

		const string sourceDir = @"D:\repo\Git\shopping-experience-develop-2\application\src";
		const string convertedSourceDir = @"D:\repo\Git\shopping-experience-develop\application\src";

		const string testDir = @"D:\repo\Git\shopping-experience-develop-2\application\tests";
		const string convertedTestDir = @"D:\repo\Git\shopping-experience-develop\application\tests";
	}
}