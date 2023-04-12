using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Gelang.Prefabify.Editor
{
    public class VirtualPrefab : IVariant
    {
        public bool Invalid { get; internal set; }
        public string InvalidReason { get; internal set; }

        public int SiblingIndex { get; set; }
        public bool Expanded { get; set; }
        public bool Selected { get; set; }
        public int SelectedIndex { get; set; }

        public IVariant Parent { get; set; } = null;
        public List<Hierarchy> Children { get; set; } = new List<Hierarchy>();
        public List<Component> AddedComponents { get; set; } = new List<Component>();
        public List<Component> RemovedComponents { get; set; } = new List<Component>();
        public List<GameObject> AddedChildren { get; set; } = new List<GameObject>();


        public Dictionary<int, Object> ReferenceCandidates { get; set; } = 
            new Dictionary<int, Object>();
        public Dictionary<Type, List<ModifiedProperty>> ModifiedProperties { get; set; } =
            new Dictionary<Type, List<ModifiedProperty>>();

        public GameObject Reference { get; set; }
        public GameObject Candidate { get; set; }

        public VirtualPrefab(GameObject reference, GameObject candidate)
        {
            Reference = reference;
            Candidate = candidate;
            SiblingIndex = candidate.transform.GetSiblingIndex();

            for (int i = 0; i < reference.transform.childCount; i++)
            {
                var referenceChild = reference.transform.GetChild(i).gameObject;
                var candidateChild = candidate.transform.GetChild(i).gameObject;
                Children.Add(new Hierarchy(this, referenceChild, candidateChild));
            }
        }

        public override string ToString()
        {
            var builder = new StringBuilder();
            builder.AppendLine($"Valid: {!Invalid}");
            if (Invalid)
                builder.AppendLine($"Reason: {InvalidReason}");
        
            builder.AppendLine($"AddedChildren: {AddedChildren.Count}");
            for (int i = 0; i < AddedChildren.Count; i++)
            {
                builder.AppendLine($" - {AddedChildren[i]}");
            }
        
            builder.AppendLine($"AddedComponents: {AddedComponents.Count}");
            for (int i = 0; i < AddedComponents.Count; i++)
            {
                builder.AppendLine($" - {AddedComponents[i]}");
            }
        
            builder.AppendLine($"RemovedComponents: {RemovedComponents.Count}");
            for (int i = 0; i < RemovedComponents.Count; i++)
            {
                builder.AppendLine($" - {RemovedComponents[i]}");
            }
        
            builder.AppendLine($"ModifiedProperties: {ModifiedProperties.Count}");
            foreach (var keyType in ModifiedProperties.Keys)
            {
                var componentList = ModifiedProperties[keyType];
                for (int i = 0; i < componentList.Count; i++)
                {
                
                    builder.AppendLine($" - {componentList[i]}");
                }
            }
        
            builder.AppendLine($"ReferenceCandidates: {ReferenceCandidates.Count}");
            foreach (var keyType in ReferenceCandidates.Keys)
            {
                builder.AppendLine($" - {keyType} : {ReferenceCandidates[keyType]}");
            }
        
            return builder.ToString();
        }
    }
}
