using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Gelang.Prefabify.Editor
{
    public static class PrefabifyUtility
    {
        #region COMMANDS
        
        [MenuItem("GameObject/Prefabify/Create Prefab", false, 101)]
        private static void CommandCreatePrefab(MenuCommand menuCommand)
        {
            if (menuCommand.context != Selection.activeObject)
                return;
            
            var savePath = EditorUtility.SaveFilePanelInProject("Save Prefab", "new Prefab", "prefab", "");
            if (savePath == string.Empty)
                return;

            var gameObject = Selection.activeGameObject;
            var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(gameObject, savePath, 
                InteractionMode.AutomatedAction, out var success);

            if (!success)
                return;
            
            var candidates = Selection.gameObjects.ToList();
            candidates.Remove(gameObject);
            
            ConvertToPrefab(prefab, candidates);
        }
        
        [MenuItem("GameObject/Prefabify/Convert to Prefab", false, 102)]
        private static void CommandConvertToPrefab(MenuCommand menuCommand)
        {
            if (menuCommand.context != Selection.activeObject)
                return;

            var path = EditorUtility.OpenFilePanel("Choose Prefab", "", "prefab");
            if (path == string.Empty)
                return;

            path = "Assets" + Path.DirectorySeparatorChar + Path.GetRelativePath(Application.dataPath, path);
            
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var candidates = Selection.gameObjects.ToList();

             ConvertToPrefab(prefab, candidates);
        }

        [PublicAPI]
        public static List<GameObject> ConvertToPrefab(GameObject existingPrefab, List<GameObject> candidates)
        {
            var selection = Selection.gameObjects;
            
            var references = FindReferences();
            var virtualPrefabs = GetGameObjectDifferences(existingPrefab, candidates);
            var prefabs = CreatePrefabVariants(existingPrefab, candidates, virtualPrefabs);

            ConnectReferences(virtualPrefabs, references);
            UpdateSelection(virtualPrefabs, prefabs, selection);
            
            return prefabs;
        }

        #endregion

        #region SELECTION

        private static void UpdateSelection(List<VirtualPrefab> variants, List<GameObject> prefabs, GameObject[] selection)
        {
            for (int i = 0; i < variants.Count; i++)
            {
                selection[variants[i].SelectedIndex] = prefabs[i];
            }

            // ReSharper disable once CoVariantArrayConversion
            Selection.objects = selection;
        }

        #endregion
        
        #region REFERENCES

        private static readonly HashSet<Type> IgnoredObjects = new HashSet<Type>() { typeof(Transform), 
            typeof(GameObject), typeof(SkinnedMeshRenderer) };
        private static readonly HashSet<string> IgnoredProperties = new HashSet<string>() { "m_GameObject", 
            "m_PrefabInstance" };
        
        private static Dictionary<int, List<SerializedProperty>> FindReferences()
        {
            var references = new Dictionary<int, List<SerializedProperty>>();
            
            // ReSharper disable once AccessToStaticMemberViaDerivedType
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                // ReSharper disable once AccessToStaticMemberViaDerivedType
                var scene = EditorSceneManager.GetSceneAt(i);
                var root = scene.GetRootGameObjects();
                
                // ReSharper disable once CoVariantArrayConversion
                var hierarchy = EditorUtility.CollectDeepHierarchy(root);
                
                for (int j = 0; j < hierarchy.Length; j++)
                {
                    var target = hierarchy[j];
                    var type = target.GetType();
                    if (IgnoredObjects.Contains(type))
                        continue;

                    var serializedObject = new SerializedObject(target);
                    var propertyIterator = serializedObject.GetIterator();
                    var containsReferences = false;
                    while (propertyIterator.Next(true))
                    {
                        if (propertyIterator.propertyType != SerializedPropertyType.ObjectReference)
                            continue;

                        var value = propertyIterator.objectReferenceValue;
                        if (value == null)
                            continue;

                        if (IgnoredProperties.Contains(propertyIterator.propertyPath))
                            continue;

                        if (value is Component reference && target is Component component)
                            if (reference.gameObject == component.gameObject)
                                continue;

                        if (AssetDatabase.Contains(value))
                            continue;

                        var instanceID = value.GetInstanceID();
                        if (!references.ContainsKey(instanceID))
                            references.Add(instanceID, new List<SerializedProperty>());

                        references[instanceID].Add(propertyIterator.Copy());

                        containsReferences = true;
                    }

                    if (!containsReferences)
                        IgnoredObjects.Add(type);
                }
            }

            return references;
        }

        private static void ConnectReferences(List<VirtualPrefab> prefabs, 
            Dictionary<int, List<SerializedProperty>> references)
        {
            foreach (var prefab in prefabs)
            {
                foreach (var reference in prefab.ReferenceCandidates.Keys)
                {
                    if(!references.ContainsKey(reference))
                        continue;

                    var properties = references[reference];
                    for (int i = 0; i < properties.Count; i++)
                    {
                        properties[i].objectReferenceValue = prefab.ReferenceCandidates[reference];
                        properties[i].serializedObject.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
            }
        }
        
        #endregion
        
        #region GATHERING

        private static List<VirtualPrefab> GetGameObjectDifferences(GameObject reference, IReadOnlyList<GameObject> candidates)
        {
            var prefabs = new List<VirtualPrefab>();
            for (int i = 0; i < candidates.Count; i++)
            {
                var potentialPrefab = GetDifferences(reference, candidates[i]);
                prefabs.Add(potentialPrefab);
            }

            return prefabs;
        }

        private static VirtualPrefab GetDifferences(GameObject reference, GameObject candidate)
        {
            var prefab = new VirtualPrefab(reference, candidate);

            if(!prefab.Invalid)
                ForEachInHierarchy(prefab, reference, candidate, FindHierarchyDifferences);
            
            if(!prefab.Invalid)
                ForEachInHierarchy(prefab, reference, candidate, CompareComponents);
            
            if(!prefab.Invalid)
                ForEachInHierarchy(prefab, reference, candidate, CompareProperties);
            
            if(!prefab.Invalid)
                ForEachInHierarchy(prefab, reference, candidate, CheckIfExpanded);

            if(!prefab.Invalid)
                ForEachInHierarchy(prefab, reference, candidate, GetReferenceCandidates);
            
            if(!prefab.Invalid)
                ForEachInHierarchy(prefab, reference, candidate, CheckIfSelected);
            
            return prefab;
        }

        private static void FindHierarchyDifferences(IVariant variant, GameObject reference, GameObject candidate)
        {
            if (variant is Hierarchy hierarchy)
            {
                for (int i = 0; i < reference.transform.childCount; i++)
                {
                    var referenceChild = reference.transform.GetChild(i);
                    var candidateChild = candidate.transform.GetChild(i);
                    
                    // TODO: IMPLEMENT BETTER COMPARISON THAN NAME CHECK
                    if (referenceChild.name != candidateChild.name)
                    {
                        hierarchy.Root.Invalid = false;
                        hierarchy.Root.InvalidReason = "Candidate Child name doesn't match Reference.";
                        return;
                    }
                }
            }
            
            if (candidate.transform.childCount > reference.transform.childCount)
            {
                for (int j = reference.transform.childCount; j < candidate.transform.childCount; j++)
                {
                    var child = candidate.transform.GetChild(j).gameObject;
                    variant.AddedChildren.Add(child);
                }
            }
        }

        private static void CompareComponents(IVariant variant, GameObject reference, GameObject candidate)
        {
            var referenceComponents = reference.GetComponents<Component>();
            var candidateComponents = candidate.GetComponents<Component>();

            if (CompareContentByType(referenceComponents, candidateComponents))
                return;

            var addedC = candidateComponents.Except(referenceComponents, TypeComparer<Component>.Instance);
            var removed = referenceComponents.Except(candidateComponents, TypeComparer<Component>.Instance);
        
            variant.AddedComponents.AddRange(addedC);
            variant.RemovedComponents.AddRange(removed);
        }
        
        private static readonly string[] IgnoredPropertyNames = {"m_FileID", "m_Name", "m_RootOrder"};
        private static readonly string[] IgnoredPropertyPaths = {"m_Children.Array.size", "m_Component.Array.size"};
        
        private static void CompareProperties(IVariant variant, GameObject reference, GameObject candidate)
        {
            var components = GetComponentPairsByType(reference, candidate);
            if(components == null)
                return;
        
            foreach (var keyType in components.Keys)
            {
                var component = components[keyType];
                var property = new SerializedObject(new[] {component.Reference, component.Candidate});
            
                var propertyIterator = property.GetIterator();
                while (propertyIterator.Next (true))
                {
                    if (!propertyIterator.hasMultipleDifferentValues) 
                        continue;
                
                    if(propertyIterator.hasChildren)
                        continue;
                    
                    var shouldIgnore = IgnoredPropertyNames.Contains(propertyIterator.name);
                    shouldIgnore |= IgnoredPropertyPaths.Contains(propertyIterator.propertyPath);

                    if (shouldIgnore) 
                        continue;
                    
                    if (!variant.ModifiedProperties.ContainsKey(keyType))
                        variant.ModifiedProperties.Add(keyType, new List<ModifiedProperty>());
                        
                    variant.ModifiedProperties[keyType].Add(new ModifiedProperty(propertyIterator, component));
                }
            }
        }

        private static void CheckIfExpanded(IVariant variant, GameObject reference, GameObject candidate)
        {
            var window = GetHierarchyWindow();
            var expanded = GetExpandedIDs(window);

            if (expanded.Contains(candidate.GetInstanceID()))
                variant.Expanded = true;
        }

        private static void GetReferenceCandidates(IVariant variant, GameObject reference, GameObject candidate)
        {
            VirtualPrefab prefab;
            if (variant is VirtualPrefab root)
            {
                prefab = root;
            }
            else
            {
                prefab = ((Hierarchy)variant).Root;
            }

            prefab.ReferenceCandidates.Add(candidate.GetInstanceID(), null);
            var components = candidate.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                prefab.ReferenceCandidates.Add(components[i].GetInstanceID(), null);
            }
        }

        private static void CheckIfSelected(IVariant variant, GameObject reference, GameObject candidate)
        {
            if (!Selection.Contains(candidate))
                return;
            
            var index = Array.IndexOf(Selection.gameObjects, candidate);
            if (index >= 0)
            {
                variant.Selected = true;
                variant.SelectedIndex = index;
            }
        }
        
        private static bool CompareContentByType<TListType>(TListType[] x, TListType[] y)
        {
            if (x.Length != y.Length)
                return false;

            for (int i = 0; i < x.Length; i++)
            {
                var contains = false;
                for (int j = 0; j < y.Length; j++)
                {
                    if (x[i].GetType() == y[j].GetType())
                        contains = true;
                }

                if (contains == false)
                    return false;
            }
        
            return true;
        }
        
        private static Dictionary<Type, ObjectPair> GetComponentPairsByType(GameObject reference, GameObject candidate)
        {
            var referenceComponents = reference.GetComponents<Component>().ToDictionary(component => component.GetType());
            var candidateComponents = candidate.GetComponents<Component>().ToDictionary(component => component.GetType());

            var components = new Dictionary<Type, ObjectPair>
            {
                { typeof(GameObject), new ObjectPair(reference, candidate) }
            };
        
            foreach (var keyType in referenceComponents.Keys)
            {
                if(!candidateComponents.ContainsKey(keyType))
                    continue;

                components[keyType] = new ObjectPair(referenceComponents[keyType], candidateComponents[keyType]);
            }

            return components;
        }
        
        private static void ForEachInHierarchy(IVariant variant, GameObject reference, GameObject candidate, 
            Action<IVariant, GameObject, GameObject> action)
        {
            action.Invoke(variant, reference, candidate);
            for (var i = 0; i < reference.transform.childCount; i++)
            {
                var referenceChild = reference.transform.GetChild(i);
                var candidateChild = candidate.transform.GetChild(i);
                ForEachInHierarchy(variant.Children[i], referenceChild.gameObject, candidateChild.gameObject, action);
            }
        }
        
        #endregion
        
        #region SETUP

        private static List<GameObject> CreatePrefabVariants(GameObject prefab, List<GameObject> candidates, 
            List<VirtualPrefab> virtualPrefabs)
        {
            var prefabs = new List<GameObject>();

            for (int i = 0; i < candidates.Count; i++)
            {
                var copy = CreatePrefabVariant(prefab, candidates[i], virtualPrefabs[i]);
                if(copy == null)
                    continue;
                
                prefabs.Add(copy);
            }
            
            return prefabs;
        }

        private static GameObject CreatePrefabVariant(GameObject prefab, GameObject candidate, VirtualPrefab virtualPrefab)
        {
            if (virtualPrefab.Invalid)
            {
                Debug.LogWarning($"Variant representing {virtualPrefab.Reference.name} isn't valid.", virtualPrefab.Reference);
                return null;
            }
            
            var copy = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            copy.transform.SetParent(candidate.transform.parent);
            copy.transform.SetSiblingIndex(virtualPrefab.SiblingIndex);
            
            SetupPrefab(copy, virtualPrefab);
            
            Object.DestroyImmediate(candidate);
            
            return copy;
        }
        
        private static void SetupPrefab(GameObject copy, IVariant variant)
        {
            DeleteRemovedComponents(copy, variant.RemovedComponents);
            CreateAddedComponents(copy, variant.AddedComponents);
            SetModifiedProperties(copy, variant.ModifiedProperties);
            TransferAddedChildren(copy, variant.AddedChildren);
            SetExpandedState(copy, variant);
            LinkReferenceCandidates(copy, variant);

            for (int i = 0; i < variant.Reference.transform.childCount; i++)
            {
                var childVariant = variant.Children[i];
                var childCopy = copy.transform.Find(childVariant.Reference.name).gameObject;
                SetupPrefab(childCopy, childVariant);
            }
        }

        private static void DeleteRemovedComponents(GameObject clone, List<Component> toRemove)
        {
            if (toRemove.Count == 0)
                return;
        
            var components = clone.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if(toRemove.Contains(component, TypeComparer<Component>.Instance))
                    Object.DestroyImmediate(component);
            }
        }
        
        private static void CreateAddedComponents(GameObject clone, List<Component> toAdd)
        {
            for (int i = 0; i < toAdd.Count; i++)
            {
                UnityEditorInternal.ComponentUtility.CopyComponent(toAdd[i]);
                UnityEditorInternal.ComponentUtility.PasteComponentAsNew(clone);
            }
        }
        
        private static void SetModifiedProperties(GameObject clone, Dictionary<Type, List<ModifiedProperty>> properties)
        {
            foreach (var keyType in properties.Keys)
            {
                SerializedObject cloneObject;
                if (keyType == typeof(GameObject))
                {
                    cloneObject = new SerializedObject(clone);
                }
                else
                {
                    var cloneComponent = clone.GetComponent(keyType);
                    cloneObject = new SerializedObject(cloneComponent);
                }
            
                var modifiedProperties = properties[keyType];
                for (int i = 0; i < modifiedProperties.Count; i++)
                {
                    var candidateObject = new SerializedObject(modifiedProperties[i].ComponentPair.Candidate);
                    var originalProperty = candidateObject.FindProperty(modifiedProperties[i].Property.propertyPath);
                    cloneObject.CopyFromSerializedProperty(originalProperty);
                    cloneObject.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }
        
        private static void TransferAddedChildren(GameObject clone, List<GameObject> variantAddedChildren)
        {
            for (int i = 0; i < variantAddedChildren.Count; i++)
            {
                variantAddedChildren[i].transform.SetParent(clone.transform);
            }
        }
        
        private static void SetExpandedState(GameObject copy, IVariant variant)
        {
            var window = GetHierarchyWindow();
            
            var method = window.GetType().GetMethod("SetExpanded",
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance, null,
                new[] { typeof(int), typeof(bool) }, null);
 
            if (method == null)
                return;
        
            method.Invoke(window, new object[] { copy.GetInstanceID(), variant.Expanded });
        }

        private static void LinkReferenceCandidates(GameObject copy, IVariant variant)
        {
            VirtualPrefab prefab;
            if (variant is VirtualPrefab root)
            {
                prefab = root;
            }
            else
            {
                prefab = ((Hierarchy)variant).Root;
            }

            prefab.ReferenceCandidates[variant.Candidate.GetInstanceID()] = copy;

            var copyComponents = copy.GetComponents<Component>();
            var referenceComponents = variant.Candidate.GetComponents<Component>();
            for (int i = 0; i < referenceComponents.Length; i++)
            {
                var oldReference = referenceComponents[i];
                var type = oldReference.GetType();
                var newReference = copyComponents.FirstOrDefault(component => component.GetType() == type);
                if (newReference == null)
                {
                    Debug.LogError("Reference component wasn't found. This shouldn't happen");
                    continue;
                }

                prefab.ReferenceCandidates[oldReference.GetInstanceID()] = newReference;
            }
        }
        
        
        
        #endregion

        #region GENERAL

        private static EditorWindow GetHierarchyWindow()
        {
            foreach (var window in Resources.FindObjectsOfTypeAll<SearchableEditorWindow>())
            {
                if (window.GetType().Name != "SceneHierarchyWindow")
                    continue;

                return window;
            }

            return null;
        }
        
        private static HashSet<int> GetExpandedIDs(EditorWindow hierarchyWindow)
        {
            var method = hierarchyWindow.GetType().GetMethod("GetExpandedIDs",
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);


            if (method == null)
                return null;

            var ids = (int[])method.Invoke(hierarchyWindow, Array.Empty<object>());
            
            return new HashSet<int>(ids);
        }
        
        #endregion
    }
}
