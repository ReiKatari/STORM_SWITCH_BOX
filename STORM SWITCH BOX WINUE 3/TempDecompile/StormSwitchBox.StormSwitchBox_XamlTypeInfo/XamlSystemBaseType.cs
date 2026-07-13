using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using Microsoft.UI.Xaml.Markup;

namespace StormSwitchBox.StormSwitchBox_XamlTypeInfo;

[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
[DebuggerNonUserCode]
internal class XamlSystemBaseType : IXamlType
{
	private string _fullName;

	private Type _underlyingType;

	public string FullName => _fullName;

	public Type UnderlyingType => _underlyingType;

	public virtual IXamlType BaseType
	{
		get
		{
			throw new NotImplementedException();
		}
	}

	public virtual IXamlMember ContentProperty
	{
		get
		{
			throw new NotImplementedException();
		}
	}

	public virtual bool IsArray
	{
		get
		{
			throw new NotImplementedException();
		}
	}

	public virtual bool IsCollection
	{
		get
		{
			throw new NotImplementedException();
		}
	}

	public virtual bool IsConstructible
	{
		get
		{
			throw new NotImplementedException();
		}
	}

	public virtual bool IsDictionary
	{
		get
		{
			throw new NotImplementedException();
		}
	}

	public virtual bool IsMarkupExtension
	{
		get
		{
			throw new NotImplementedException();
		}
	}

	public virtual bool IsBindable
	{
		get
		{
			throw new NotImplementedException();
		}
	}

	public virtual bool IsReturnTypeStub
	{
		get
		{
			throw new NotImplementedException();
		}
	}

	public virtual bool IsLocalType
	{
		get
		{
			throw new NotImplementedException();
		}
	}

	public virtual IXamlType ItemType
	{
		get
		{
			throw new NotImplementedException();
		}
	}

	public virtual IXamlType KeyType
	{
		get
		{
			throw new NotImplementedException();
		}
	}

	public virtual IXamlType BoxedType
	{
		get
		{
			throw new NotImplementedException();
		}
	}

	public XamlSystemBaseType(string fullName, Type underlyingType)
	{
		_fullName = fullName;
		_underlyingType = underlyingType;
	}

	public virtual IXamlMember GetMember(string name)
	{
		throw new NotImplementedException();
	}

	public virtual object ActivateInstance()
	{
		throw new NotImplementedException();
	}

	public virtual void AddToMap(object instance, object key, object item)
	{
		throw new NotImplementedException();
	}

	public virtual void AddToVector(object instance, object item)
	{
		throw new NotImplementedException();
	}

	public virtual void RunInitializer()
	{
		throw new NotImplementedException();
	}

	public virtual object CreateFromString(string input)
	{
		throw new NotImplementedException();
	}
}
