using UnityEngine;

namespace Gelang.Prefabify.Editor
{
    public class ObjectPair
    {
        public Object Reference { get; }
        public Object Candidate { get; }

        public ObjectPair(Object reference, Object candidate)
        {
            Reference = reference;
            Candidate = candidate;
        }
    }
}
