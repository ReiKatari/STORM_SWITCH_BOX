using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml.Markup;

namespace StormSwitchBox.StormSwitchBox_XamlTypeInfo;

[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
[DebuggerNonUserCode]
internal class XamlUserType : XamlSystemBaseType, IXamlType
{
	private XamlTypeInfoProvider _provider;

	private IXamlType _baseType;

	private IXamlType _boxedType;

	private bool _isArray;

	private bool _isMarkupExtension;

	private bool _isBindable;

	private bool _isReturnTypeStub;

	private bool _isLocalType;

	private string _contentPropertyName;

	private string _itemTypeName;

	private string _keyTypeName;

	private Dictionary<string, string> _memberNames;

	private Dictionary<string, object> _enumValues;

	public override IXamlType BaseType => _baseType;

	public override bool IsArray => _isArray;

	public override bool IsCollection => CollectionAdd != null;

	public override bool IsConstructible => Activator != null;

	public override bool IsDictionary => DictionaryAdd != null;

	public override bool IsMarkupExtension => _isMarkupExtension;

	public override bool IsBindable => _isBindable;

	public override bool IsReturnTypeStub => _isReturnTypeStub;

	public override bool IsLocalType => _isLocalType;

	public override IXamlType BoxedType => _boxedType;

	public override IXamlMember ContentProperty => _provider.GetMemberByLongName(_contentPropertyName);

	public override IXamlType ItemType => _provider.GetXamlTypeByName(_itemTypeName);

	public override IXamlType KeyType => _provider.GetXamlTypeByName(_keyTypeName);

	public Activator Activator { get; set; }

	public AddToCollection CollectionAdd { get; set; }

	public AddToDictionary DictionaryAdd { get; set; }

	public CreateFromStringMethod CreateFromStringMethod { get; set; }

	public BoxInstanceMethod BoxInstance { get; set; }

	public XamlUserType(XamlTypeInfoProvider provider, string fullName, Type fullType, IXamlType baseType)
		: base(fullName, fullType)
	{
		_provider = provider;
		_baseType = baseType;
	}

	public override IXamlMember GetMember(string name)
	{
		if (_memberNames == null)
		{
			return null;
		}
		if (_memberNames.TryGetValue(name, out var value))
		{
			return _provider.GetMemberByLongName(value);
		}
		return null;
	}

	public override object ActivateInstance()
	{
		return Activator();
	}

	public override void AddToMap(object instance, object key, object item)
	{
		DictionaryAdd(instance, key, item);
	}

	public override void AddToVector(object instance, object item)
	{
		CollectionAdd(instance, item);
	}

	public override void RunInitializer()
	{
		RuntimeHelpers.RunClassConstructor(base.UnderlyingType.TypeHandle);
	}

	public override object CreateFromString(string input)
	{
		if (BoxedType != null)
		{
			return BoxInstance(BoxedType.CreateFromString(input));
		}
		if (CreateFromStringMethod != null)
		{
			return CreateFromStringMethod(input);
		}
		if (_enumValues != null)
		{
			long num = 0L;
			string[] array = input.Split(',');
			string[] array2 = array;
			foreach (string text in array2)
			{
				long num2 = 0L;
				try
				{
					if (_enumValues.TryGetValue(text.Trim(), out var value))
					{
						num2 = Convert.ToInt64(value);
					}
					else
					{
						try
						{
							num2 = Convert.ToInt64(text.Trim());
						}
						catch (FormatException)
						{
							foreach (string key in _enumValues.Keys)
							{
								if (string.Compare(text.Trim(), key, StringComparison.OrdinalIgnoreCase) == 0 && _enumValues.TryGetValue(key.Trim(), out value))
								{
									num2 = Convert.ToInt64(value);
									break;
								}
							}
						}
					}
					num |= num2;
				}
				catch (FormatException)
				{
					throw new ArgumentException(input, base.FullName);
				}
			}
			return Convert.ChangeType(num, Enum.GetUnderlyingType(base.UnderlyingType));
		}
		throw new ArgumentException(input, base.FullName);
	}

	public void SetContentPropertyName(string contentPropertyName)
	{
		_contentPropertyName = contentPropertyName;
	}

	public void SetIsArray()
	{
		_isArray = true;
	}

	public void SetIsMarkupExtension()
	{
		_isMarkupExtension = true;
	}

	public void SetIsBindable()
	{
		_isBindable = true;
	}

	public void SetIsReturnTypeStub()
	{
		_isReturnTypeStub = true;
	}

	public void SetIsLocalType()
	{
		_isLocalType = true;
	}

	public void SetItemTypeName(string itemTypeName)
	{
		_itemTypeName = itemTypeName;
	}

	public void SetKeyTypeName(string keyTypeName)
	{
		_keyTypeName = keyTypeName;
	}

	public void SetBoxedType(IXamlType boxedType)
	{
		_boxedType = boxedType;
	}

	public object BoxType<T>(object instance) where T : struct
	{
		T val = (T)instance;
		return val;
	}

	public void AddMemberName(string shortName)
	{
		if (_memberNames == null)
		{
			_memberNames = new Dictionary<string, string>();
		}
		_memberNames.Add(shortName, base.FullName + "." + shortName);
	}

	public void AddEnumValue(string name, object value)
	{
		if (_enumValues == null)
		{
			_enumValues = new Dictionary<string, object>();
		}
		_enumValues.Add(name, value);
	}
}
