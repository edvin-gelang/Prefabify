using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gelang.Prefabify.Editor
{
    public interface IVariant
    {
        public IVariant Parent { get; set; }
        public List<Hierarchy> Children { get; set; }
    
        public bool Expanded { get; set; }
        public bool Selected { get; set; }
        public int SelectedIndex { get; set; }
    
        public List<Component> AddedComponents  { get; set; }
        public List<Component> RemovedComponents  { get; set; }
        public List<GameObject> AddedChildren  { get; set; }
        public Dictionary<Type, List<ModifiedProperty>> ModifiedProperties  { get; set; }
    
        public GameObject Reference { get; set; }
        public GameObject Candidate { get; set; }
    
    }
}