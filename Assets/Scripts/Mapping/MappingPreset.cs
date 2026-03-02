// Assets/Scripts/Mapping/MappingPreset.cs
using System.Collections.Generic;
using UnityEngine;

namespace MappingTool
{
    [CreateAssetMenu(menuName = "Mapping/Mapping Preset", fileName = "MappingPreset")]
    public class MappingPreset : ScriptableObject
    {
        public MappingCondition condition;
        public List<MappingBinding> bindings = new List<MappingBinding>();
    }
}
