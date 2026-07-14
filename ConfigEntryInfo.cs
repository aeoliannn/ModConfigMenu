using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;

[assembly: AssemblyVersion("1.0.0")]
[assembly: AssemblyFileVersion("1.0.0")]

namespace ModConfigMenu
{
    internal enum EntryControlType
    {
        Bool,
        Int,
        Float,
        Textbox,
        Dropdown,
        Keybind
    }

    internal class EntryInfo
    {
        public ConfigDefinition Def;
        public ConfigEntryBase Entry;
        public EntryControlType ControlType;
        public string Tooltip;

        // Int/Float range - only enforced when HasRange is true
        public bool HasRange;
        public object MinValue;
        public object MaxValue;

        // Dropdown choices
        public string[] Choices;

        // Keybind conflict warning (set during scan)
        public string ConflictWarning;
    }

    internal class ModInfo
    {
        public string Guid;
        public string Name;
        public ConfigFile ConfigFile;
        public List<EntryInfo> Entries = new List<EntryInfo>();
        public List<string> Sections = new List<string>();
        public bool HasKeybindConflicts;
    }
}
