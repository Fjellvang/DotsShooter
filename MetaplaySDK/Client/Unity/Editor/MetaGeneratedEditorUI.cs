// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using Metaplay.Core.Config;
using Metaplay.Core.Forms;
using Metaplay.Core.Serialization;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
#if UNITY_2021
using UnityEditor.UIElements;
#endif

namespace Metaplay.Unity
{
    public class MetaGeneratedEditorUI : IMetaIntegrationSingleton<MetaGeneratedEditorUI>
    {
        protected MetaDictionary<Type, Type> Drawers = new()
        {
            {typeof(string), typeof(StringInputMetaMemberDrawer)},
            {typeof(bool), typeof(BasicFieldMetaMemberDrawer<Toggle, bool>)},
            {typeof(int), typeof(BasicFieldMetaMemberDrawer<IntegerField, int>)},
            {typeof(float), typeof(BasicFieldMetaMemberDrawer<FloatField, float>)},
            {typeof(object), typeof(ObjectMetaMemberDrawer)}
        };

        protected MetaDictionary<Func<MetaSerializableMember, Type, bool>, Type> ConditionalDrawers = new()
        {
            {(member, type) => member.Name == nameof(GoogleSheetBuildSource.SpreadsheetId) && type == typeof(string), typeof(SpreadsheetIdInputMetaMemberDrawer) }
        };


        public virtual void RegisterDrawer(Func<MetaSerializableMember, Type, bool> predicate, Type drawerType)
        {
            ConditionalDrawers[predicate] = drawerType;
        }

        public virtual void RegisterDrawer(Type memberType, Type drawerType)
        {
            Drawers[memberType] = drawerType;
        }

        public virtual bool RemoveDrawer(Type memberType)
        {
            return Drawers.Remove(memberType);
        }

        protected virtual bool TryGetDrawerTypeForMemberType(MetaSerializableMember member, Type memberType, out Type drawerType)
        {
            foreach ((Func<MetaSerializableMember, Type, bool> func, Type type) in ConditionalDrawers)
            {
                if (func(member, memberType))
                {
                    drawerType = type;
                    return true;
                }
            }

            if (Drawers.TryGetValue(memberType, out drawerType))
                return true;

            if (typeof(GameConfigBuildSource).IsAssignableFrom(memberType))
            {
                drawerType = typeof(GameConfigBuildSourceMetaMemberDrawer);
                return true;
            }

            if (memberType.IsEnum)
            {
                drawerType = typeof(EnumMetaMemberDrawer);
                return true;
            }

            if (MetaSerializerTypeRegistry.TryGetTypeSpec(memberType, out MetaSerializableType typeSpec))
            {
                if ((typeSpec.IsAbstract && typeSpec.DerivedTypes.Count > 0) || typeSpec.IsObject)
                {
                    drawerType = typeof(AbstractMetaMemberDrawer);
                    return true;
                }
            }

            return false;
        }

        public virtual VisualElement CreateGUIForType(Type type, object instance, Action<object> valueChanged)
        {
            VisualElement element = new VisualElement();
            element.AddToClassList("unity-base-field__aligned");

            Dictionary<MetaSerializableMember, object> values = new Dictionary<MetaSerializableMember, object>();

            MetaSerializableType spec = MetaSerializerTypeRegistry.GetTypeSpec(type);
            foreach (MetaSerializableMember member in spec.Members.Select((x, i) => (member: x, index: i)).OrderBy(x => x.member.MemberInfo.GetCustomAttribute<MetaFormLayoutOrderHintAttribute>()?.Order ?? x.index + 1).Select(x => x.member))
            {
                values[member] = member.GetValue(instance);
                VisualElement visualElement = CreateGUIForMember(member, member.Type, instance,
                    x =>
                    {
                        values[member] = x;

                        object obj = MetaSerializationUtil.CreateAndPopulate(spec, values);
                        valueChanged?.Invoke(obj);
                    });
                if(visualElement != null)
                    element.Add(visualElement);
            }

            return element;
        }

        protected virtual VisualElement CreateGUIForMember(MetaSerializableMember member, Type type, object instance, Action<object> valueChanged)
        {
            if (TryGetDrawerTypeForMemberType(member, type, out Type drawerType))
            {
                MetaMemberDrawer drawer = (MetaMemberDrawer)Activator.CreateInstance(
                    drawerType,
                    BindingFlags.Default,
                    null,
                    args: new[]
                    {
                        member,
                        instance
                    },
                    CultureInfo.CurrentCulture);
                drawer.ValueChanged += valueChanged.Invoke;
                return drawer.CreateGUI();
            }

            return null;
        }
    }

    /// Base class for the MetaMember rendering layer, currently has very minimal support and
    /// is only used by <see cref="GameConfigBuildWindow"/> and is not intended to be used by anything else at the moment.
    public abstract class MetaMemberDrawer
    {
        protected MetaSerializableMember _member;
        protected object                 _instance;

        public event Action<object> ValueChanged;

        protected MetaMemberDrawer(MetaSerializableMember member, object instance)
        {
            _member   = member;
            _instance = instance;
        }

        public abstract VisualElement CreateGUI();

        protected void InvokeValueChanged(object newValue)
        {
            ValueChanged?.Invoke(newValue);
        }
    }

    public abstract class MetaMemberDrawer<T> : MetaMemberDrawer
    {
        protected MetaMemberDrawer(MetaSerializableMember member, object instance) : base(member, instance) { }

        public string GetDisplayName()
        {
            MetaFormDisplayPropsAttribute attribute = _member.MemberInfo.GetCustomAttribute<MetaFormDisplayPropsAttribute>();
            if (attribute != null)
                return ObjectNames.NicifyVariableName(attribute.DisplayName);

            return ObjectNames.NicifyVariableName(_member.Name);
        }

        public string GetPlaceholder()
        {
            MetaFormDisplayPropsAttribute attribute = _member.MemberInfo.GetCustomAttribute<MetaFormDisplayPropsAttribute>();
            if (attribute != null)
                return attribute.DisplayPlaceholder;

            return "";
        }

        public string GetHint()
        {
            MetaFormDisplayPropsAttribute attribute = _member.MemberInfo.GetCustomAttribute<MetaFormDisplayPropsAttribute>();
            if (attribute != null)
                return attribute.DisplayHint;

            return "";
        }

        public virtual void SetValue(T newValue)
        {
            InvokeValueChanged(newValue);
        }

        public virtual T GetValue()
        {
            return (T)_member.GetValue(_instance);
        }
    }

    public class ObjectMetaMemberDrawer : MetaMemberDrawer<object>
    {
        public ObjectMetaMemberDrawer(MetaSerializableMember member, object instance) : base(member, instance) { }

        public override VisualElement CreateGUI()
        {
            VisualElement element = new VisualElement();
            element.Add(new Label(ObjectNames.NicifyVariableName(_member.Name)) {style = {unityFontStyleAndWeight = FontStyle.Bold}});

            element.Add(IntegrationRegistry.Get<MetaGeneratedEditorUI>().CreateGUIForType(_member.Type, _member.GetValue(_instance), InvokeValueChanged));

            return element;
        }
    }

    public class GameConfigBuildSourceMetaMemberDrawer : MetaMemberDrawer<GameConfigBuildSource>
    {
        class CustomSource
        {
            public Type                  Type;
            public GameConfigBuildSource Instance;
        }

        List<GameConfigBuildSource> _definedSources;
        List<CustomSource>          _customSources;

        public GameConfigBuildSourceMetaMemberDrawer(MetaSerializableMember member, object instance) : base(member, instance) { }

        int FindSourceIdx(GameConfigBuildSource source)
        {
            if (source == null)
                return -1;

            int idx = _definedSources.IndexOf(source);
            if (idx != -1)
                return idx;

            idx = _customSources.FindIndex(x => x.Type == source.GetType());
            if (idx != -1)
                return _definedSources.Count + idx;

            return -1;
        }

        GameConfigBuildSource CreateCustomSource(Type type)
        {
            if (type == typeof(GoogleSheetBuildSource))
                return new GoogleSheetBuildSource("", "");
            if (type == typeof(FileSystemBuildSource))
                return new FileSystemBuildSource(FileSystemBuildSource.Format.Csv);

            return null;
        }

        void UpdateCustomEditingUI(VisualElement parent, GameConfigBuildSource customSource)
        {
            VisualElement prevElement = parent.Query(classes: "custom-source-contents");
            if (prevElement != null)
                parent.Remove(prevElement);
            if (customSource != null)
            {
                VisualElement element = IntegrationRegistry.Get<MetaGeneratedEditorUI>().CreateGUIForType(customSource.GetType(), customSource, InvokeValueChanged);
                element.AddToClassList("custom-source-contents");
                element.AddToClassList(Toggle.alignedFieldUssClassName);
                element.style.paddingLeft = 16;
                parent.Add(element);
            }
        }

        public override VisualElement CreateGUI()
        {
            IUnityGameConfigBuildIntegration integration = IntegrationRegistry.Get<IUnityGameConfigBuildIntegration>();

            _definedSources = integration.GetAvailableGameConfigBuildSources(_member.Name).ToList();
            _customSources  = integration.GetCustomBuildSourceTypesForSource(_member.Name).Where(x => _member.Type.IsAssignableFrom(x)).Select(
                x => new CustomSource()
                {
                    Type     = x,
                    Instance = null
                }).ToList();
            List<string> options = _definedSources.Select(x => x.DisplayName).Concat(_customSources.Select(x => $"Custom {x.Type.Name}")).ToList();

            GameConfigBuildSource gameConfigBuildSource = GetValue();
            int index = FindSourceIdx(gameConfigBuildSource);

            // Clear current value if no match found
            if (index == -1 && gameConfigBuildSource != null)
            {
                SetValue(null);
                gameConfigBuildSource = null;
            }

            // Select first option
            if (gameConfigBuildSource == null && options.Count > 0)
            {
                index = 0;
                if (index < _definedSources.Count)
                    gameConfigBuildSource = _definedSources[index];
                else
                    gameConfigBuildSource = CreateCustomSource(_customSources[index].Type);
                SetValue(gameConfigBuildSource);
            }

            if (index >= _definedSources.Count)
            {
                // Reuse instance for custom type
                _customSources[index - _definedSources.Count].Instance = gameConfigBuildSource;
            }

            // Create UI elements

            VisualElement visualElement = new VisualElement();
            if (options.Count > 1)
            {
                DropdownField sourceSelect  = new DropdownField();
                visualElement.Add(sourceSelect);
                sourceSelect.choices                        = options;
                sourceSelect.label                          = ObjectNames.NicifyVariableName(GetDisplayName());
                sourceSelect.index                          = index;
                sourceSelect.labelElement.style.paddingTop  = 0;
                sourceSelect.labelElement.style.paddingLeft = 0;
                sourceSelect.AddToClassList(Toggle.alignedFieldUssClassName);
                sourceSelect.RegisterValueChangedCallback(
                    x =>
                    {
                        if (sourceSelect.index >= 0)
                        {
                            GameConfigBuildSource selectedSource = null;

                            // Find instance for the selected source
                            if (sourceSelect.index >= _definedSources.Count)
                            {
                                CustomSource customSource = _customSources[sourceSelect.index - _definedSources.Count];
                                customSource.Instance ??= CreateCustomSource(customSource.Type);
                                selectedSource        =   customSource.Instance;
                            }
                            else
                            {
                                selectedSource = _definedSources[sourceSelect.index];
                            }

                            SetValue(selectedSource);

                            // Update edit UI
                            UpdateCustomEditingUI(visualElement, sourceSelect.index >= _definedSources.Count ? selectedSource : null);
                        }
                    });
            }
            else if (options.Count == 1)
            {
                TextField textField = new TextField();
                textField.isReadOnly = true;
                textField.label      = ObjectNames.NicifyVariableName(GetDisplayName());

                textField.AddToClassList(Toggle.alignedFieldUssClassName);

                textField.labelElement.style.paddingTop = 0;
                textField.labelElement.style.paddingLeft = 0;
                textField.value                         = options[0];
                visualElement.Add(textField);
            }

            // Update edit UI
            UpdateCustomEditingUI(visualElement, index >= _definedSources.Count ? gameConfigBuildSource : null);

            return visualElement;
        }
    }

    public class AbstractMetaMemberDrawer : MetaMemberDrawer<object>
    {
        public AbstractMetaMemberDrawer(MetaSerializableMember member, object instance) : base(member, instance) { }

        public override VisualElement CreateGUI()
        {
            if (!MetaSerializerTypeRegistry.TryGetTypeSpec(_member.Type, out MetaSerializableType typeSpec))
                return null;

            Foldout foldout = new Foldout() {text = GetDisplayName()};

            foldout.RegisterValueChangedCallback(
                x =>
                {
                    if (x.newValue && foldout.childCount == 0)
                        CreateObjectUI(typeSpec, foldout);
                });

            if (foldout.value && foldout.childCount == 0)
                CreateObjectUI(typeSpec, foldout);

            return foldout;
        }

        void CreateObjectUI(MetaSerializableType typeSpec, Foldout foldout)
        {
            VisualElement objectInstanceContainer = new VisualElement();

            Type   currentType     = GetValue()?.GetType() ?? typeSpec.DerivedTypes?.First().Value ?? typeSpec.Type;
            object currentInstance = GetValue() ?? MetaSerializationUtil.CreateAndPopulate(MetaSerializerTypeRegistry.GetTypeSpec(currentType), new Dictionary<MetaSerializableMember, object>());

            if (typeSpec.DerivedTypes?.Count > 1)
            {
                DropdownField dropdownField = new DropdownField(typeSpec.DerivedTypes.Select(x => x.Value.Name).ToList(), 0);

                dropdownField.value = currentType.Name;
                dropdownField.RegisterValueChangedCallback(
                    x =>
                    {
                        Type                 value        = typeSpec.DerivedTypes.FirstOrDefault(y => y.Value.Name == x.newValue).Value;
                        MetaSerializableType selectedType = MetaSerializerTypeRegistry.GetTypeSpec(value);
                        object               obj          = MetaSerializationUtil.CreateAndPopulate(selectedType, new Dictionary<MetaSerializableMember, object>());
                        SetValue(obj);

                        for (int i = 0; i < objectInstanceContainer.childCount; i++)
                            objectInstanceContainer.RemoveAt(0);

                        objectInstanceContainer.Add(
                            IntegrationRegistry.Get<MetaGeneratedEditorUI>().CreateGUIForType(
                                currentType,
                                currentInstance,
                                SetValue));
                    });

                dropdownField.label   = "Type";
                dropdownField.tooltip = GetHint();
                dropdownField.choices = typeSpec.DerivedTypes.Select(x => x.Value.Name).ToList();

                dropdownField.labelElement.style.paddingTop  = 0;
                dropdownField.labelElement.style.paddingLeft = 0;
                dropdownField.AddToClassList(Toggle.alignedFieldUssClassName);

                foldout.Add(dropdownField);
            }

            foldout.Add(objectInstanceContainer);

            objectInstanceContainer.Add(
                IntegrationRegistry.Get<MetaGeneratedEditorUI>().CreateGUIForType(
                    currentType,
                    currentInstance,
                    SetValue));
        }
    }

    public class EnumMetaMemberDrawer : MetaMemberDrawer<Enum>
    {
        public EnumMetaMemberDrawer(MetaSerializableMember member, object instance) : base(member, instance) { }

        public override VisualElement CreateGUI()
        {
            DropdownField dropdownField = new DropdownField();

            dropdownField.value = GetValue().ToString();
            dropdownField.RegisterValueChangedCallback(x => SetValue((Enum)Enum.Parse(_member.Type,  x.newValue)));

            dropdownField.label   = GetDisplayName();
            dropdownField.tooltip = GetHint();
            dropdownField.choices = Enum.GetNames(_member.Type).ToList();

            dropdownField.labelElement.style.paddingTop  = 0;
            dropdownField.labelElement.style.paddingLeft = 0;
            dropdownField.AddToClassList(Toggle.alignedFieldUssClassName);

            return dropdownField;
        }
    }

    public class StringInputMetaMemberDrawer : BasicFieldMetaMemberDrawer<TextField, string>
    {
        public StringInputMetaMemberDrawer(MetaSerializableMember member, object instance) : base(member, instance) { }

        public override VisualElement CreateGUI()
        {
            TextField textField = base.CreateGUI() as TextField;
            textField.SetPlaceholderText(GetPlaceholder());

            textField.labelElement.style.paddingTop  = 0;
            textField.labelElement.style.paddingLeft = 0;

            return textField;
        }
    }

    public class BoolInputMetaMemberDrawer : BasicFieldMetaMemberDrawer<Toggle, bool>
    {
        public BoolInputMetaMemberDrawer(MetaSerializableMember member, object instance) : base(member, instance)
        {
        }
    }

    public class FloatInputMetaMemberDrawer : BasicFieldMetaMemberDrawer<FloatField, float>
    {
        public FloatInputMetaMemberDrawer(MetaSerializableMember member, object instance) : base(member, instance)
        {
        }
    }

    public class IntInputMetaMemberDrawer : BasicFieldMetaMemberDrawer<IntegerField, int>
    {
        public IntInputMetaMemberDrawer(MetaSerializableMember member, object instance) : base(member, instance)
        {
        }
    }

    public class SpreadsheetIdInputMetaMemberDrawer : StringInputMetaMemberDrawer
    {
        public SpreadsheetIdInputMetaMemberDrawer(MetaSerializableMember member, object instance) : base(member, instance)
        {
        }

        public override void SetValue(string newValue)
        {
            base.SetValue(GameConfigHelper.ParseIdFromSpreadsheetUrlOrId(newValue));
        }
    }

    public class BasicFieldMetaMemberDrawer<TFieldType, TValueType> : MetaMemberDrawer<TValueType> where TFieldType : BaseField<TValueType>, new()
    {
        public BasicFieldMetaMemberDrawer(MetaSerializableMember member, object instance) : base(member, instance) { }

        public override VisualElement CreateGUI()
        {
            TFieldType integerField = new TFieldType();

            integerField.labelElement.style.paddingLeft = 0;

            integerField.value = GetValue();
            integerField.RegisterValueChangedCallback(x => SetValue(x.newValue));

            integerField.label   = GetDisplayName();
            integerField.tooltip = GetHint();

            integerField.AddToClassList(Toggle.alignedFieldUssClassName);
            return integerField;
        }
    }
}
