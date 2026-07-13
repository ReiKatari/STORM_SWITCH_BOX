using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using Microsoft.UI.Xaml.Markup;
using Windows.Foundation.Metadata;

namespace StormSwitchBox.StormSwitchBox_XamlTypeInfo;

[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
[DebuggerNonUserCode]
public sealed class XamlMetaDataProvider : IXamlMetadataProvider
{
	private XamlTypeInfoProvider _provider = null;

	private XamlTypeInfoProvider Provider
	{
		get
		{
			if (_provider == null)
			{
				_provider = new XamlTypeInfoProvider();
			}
			return _provider;
		}
	}

	[DefaultOverload]
	public IXamlType GetXamlType(Type type)
	{
		return Provider.GetXamlTypeByType(type);
	}

	public IXamlType GetXamlType(string fullName)
	{
		return Provider.GetXamlTypeByName(fullName);
	}

	public XmlnsDefinition[] GetXmlnsDefinitions()
	{
		return new XmlnsDefinition[0];
	}
}
