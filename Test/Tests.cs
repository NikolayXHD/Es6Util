using NUnit.Framework;

namespace Es6Util.Test
{
	[TestFixture]
	public class Es6ConverterTests
	{
		[Test]
		public void Minimalistic_module_Is_converted()
		{
			const string source = @"define(function () {
	return {somefield: 'somevalue'};
});";

			string converted = Es6Converter.ConvertToEs6(source);

			const string expectedResult = "export default {somefield: 'somevalue'};\r\n";
			Assert.That(converted, Is.EqualTo(expectedResult));
		}

		[Test]
		public void Opening_use_strict_directive_Is_removed()
		{
			const string source = @"define(function () {
	'use strict';

	return {somefield: 'somevalue'};
});";

			string converted = Es6Converter.ConvertToEs6(source);

			const string expectedResult = "export default {somefield: 'somevalue'};\r\n";
			Assert.That(converted, Is.EqualTo(expectedResult));
		}

		[Test]
		public void Nested_return_directive_Is_not_confused_with_export()
		{
			const string source = @"define(function () {
	return {
		somefield: function() {
			return null;
		}
	};
});";

			string converted = Es6Converter.ConvertToEs6(source);

			const string expectedResult = @"export default {
	somefield: function() {
		return null;
	}
};
";
			Assert.That(converted, Is.EqualTo(expectedResult));
		}

		[Test]
		public void Nested_use_strict_directive_Is_not_removed()
		{
			const string source = @"define(function () {
	return {
		somefield: function() {
			'use strict';
		}
	};
});";

			string converted = Es6Converter.ConvertToEs6(source);

			const string expectedResult = @"export default {
	somefield: function() {
		'use strict';
	}
};
";
			Assert.That(converted, Is.EqualTo(expectedResult));
		}

		[Test]
		public void One_module_import_Is_converted()
		{
			const string source = @"define(['somemodule'], function (somemodule) {
	return {somefield: 'somevalue'};
});";

			string converted = Es6Converter.ConvertToEs6(source);

			const string expectedResult = @"import somemodule from 'somemodule';

export default {somefield: 'somevalue'};
";
			Assert.That(converted, Is.EqualTo(expectedResult));
		}

		[Test]
		public void Two_module_imports_Are_converted()
		{
			const string source = @"define([
	'm1',
	'm2'], function (
	m1,
	m2) {
	return {somefield: 'somevalue'};
});";

			string converted = Es6Converter.ConvertToEs6(source);

			const string expectedResult = @"import m1 from 'm1';
import m2 from 'm2';

export default {somefield: 'somevalue'};
";
			Assert.That(converted, Is.EqualTo(expectedResult));
		}

		[Test]
		public void Empty_line_between_modules_Is_preserved()
		{
			const string source = @"define([
	'm1',

	'm2'], function (
	m1,

	m2) {
	return {somefield: 'somevalue'};
});";

			string converted = Es6Converter.ConvertToEs6(source);

			const string expectedResult = @"import m1 from 'm1';

import m2 from 'm2';

export default {somefield: 'somevalue'};
";
			Assert.That(converted, Is.EqualTo(expectedResult));
		}
	}
}