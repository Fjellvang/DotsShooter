// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using System;
using System.Reflection;

namespace Metaplay.Unity
{
    internal class MenuWrapper
    {
        public const BindingFlags All   = (BindingFlags)60 /*Instance|Static|Public|NonPublic*/;
        static       Type         _menu = typeof(UnityEditor.Menu);
        static       Type         _util = typeof(UnityEditor.EditorUtility);

        static readonly MethodInfo _addMenuItemMethod = TryGetMethod(_menu, "AddMenuItem");

        public static void AddMenuItem(string name, string shortcut, bool @checked, int priority, Action execute, Func<bool> validate)
        {
            _addMenuItemMethod?.Invoke(null, new object[] { name, shortcut, @checked, priority, execute, validate });
        }

        static readonly MethodInfo _removeMenuItemMethod = TryGetMethod(_menu, "RemoveMenuItem");

        public static void RemoveMenuItem(string name)
        {
            _removeMenuItemMethod?.Invoke(null, new object[] {name});
        }

        static readonly MethodInfo _menuItemExistsMethod = TryGetMethod(_menu, "MenuItemExists");

        public static bool MenuItemExists(string menuPath)
        {
            if (_menuItemExistsMethod != null)
                return (bool)_menuItemExistsMethod.Invoke(null, new object[] {menuPath});

            return false;
        }

        static readonly MethodInfo _rebuildMenuItemsMethod = TryGetMethod(_menu, "RebuildAllMenus");

        public static void RebuildAllMenus()
        {
            _rebuildMenuItemsMethod?.Invoke(null, Array.Empty<object>());
        }

        static readonly MethodInfo _updateAllMenusMethod = TryGetMethod(_util, "Internal_UpdateAllMenus");

        public static void UpdateAllMenus()
        {
            _updateAllMenusMethod?.Invoke(null, null);
        }

        static MethodInfo TryGetMethod(Type type, string methodName)
        {
            try
            {
                MethodInfo mi = type.GetMethod(methodName, All);
                if (mi != null)
                    return mi;
            }
            catch (Exception)
            {
            }
            DebugLog.Error($"Could not find method '{methodName}', Unity API likely changed. This is a bug, please report this to Metaplay.");
            return null;
        }
    }
}
