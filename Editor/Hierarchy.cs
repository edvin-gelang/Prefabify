using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gelang.Prefabify.Editor
{
    public class Hierarchy : IVariant
    {
        public VirtualPrefab Root { get; set; }
        public IVariant Parent { get; set; }
        public List<Hierarchy> Children { get; set; } = new List<Hierarchy>();
    
        public bool Expanded { get; set; }
        public bool Selected { get; set; }
        public int SelectedIndex { get; set; }

        public List<Component> AddedComponents { get; set; } = new List<Component>();
        public List<Component> RemovedComponents { get; set; } = new List<Component>();
        public List<GameObject> AddedChildren { get; set; } = new List<GameObject>();

        public Dictionary<Type, List<ModifiedProperty>> ModifiedProperties { get; set; } =
            new Dictionary<Type, List<ModifiedProperty>>();
    
        public GameObject Reference { get; set; }
        public GameObject Candidate { get; set; }

        public Hierarchy(IVariant parent, GameObject reference, GameObject candidate)
        {
            Reference = reference;
            Candidate = candidate;
            Parent = parent;

            if (parent is VirtualPrefab root)
            {
                Root = root;
            }
            else if(parent is Hierarchy hierarchy)
            {
                Root = hierarchy.Root;
            }
        
            for (int i = 0; i < reference.transform.childCount; i++)
            {
                var referenceChild = reference.transform.GetChild(i).gameObject;
                var candidateChild = candidate.transform.GetChild(i).gameObject;
                Children.Add(new Hierarchy(this, referenceChild, candidateChild));
            }
        }
    }
}