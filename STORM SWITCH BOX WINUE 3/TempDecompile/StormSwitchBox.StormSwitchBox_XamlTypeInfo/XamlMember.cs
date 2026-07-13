using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using Microsoft.UI.Xaml.Markup;

namespace StormSwitchBox.StormSwitchBox_XamlTypeInfo;

[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
[DebuggerNonUserCode]
internal class XamlMember : IXamlMember
{
	private XamlTypeInfoProvider _provider;

	private string _name;

	private bool _isAttachable;

	private bool _isDependencyProperty;

	private bool _isReadOnly;

	private string _typeName;

	private string _targetTypeName;

	public string Name => _name;

	public IXamlType Type => _provider.GetXamlTypeByName(_typeName);

	public IXamlType TargetType => _provider.GetXamlTypeByName(_targetTypeName);

	public bool IsAttachable => _isAttachable;

	public bool IsDependencyProperty => _isDependencyProperty;

	public bool IsReadOnly => _isReadOnly;

	public Getter Getter { get; set; }

	public Setter Setter { get; set; }

	public XamlMember(XamlTypeInfoProvider provider, string name, string typeName)
	{
		_name = name;
		_typeName = typeName;
		_provider = provider;
	}

	public void SetTargetTypeName(string targetTypeName)
	{
		_targetTypeName = targetTypeName;
	}

	public void SetIsAttachable()
	{
		_isAttachable = true;
	}

	public void SetIsDependencyProperty()
	{
		_isDependencyProperty = true;
	}

	public void SetIsReadOnly()
	{
		_isReadOnly = true;
	}

	public object GetValue(object instance)
	{
		if (Getter != null)
		{
			return Getter(instance);
		}
		throw new InvalidOperationException("GetValue");
	}

	public void SetValue(object instance, object value)
	{
		if (Setter != null)
		{
			Setter(instance, value);
			return;
		}
		throw new InvalidOperationException("SetValue");
	}
}
