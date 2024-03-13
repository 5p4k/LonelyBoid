using UnityEngine;

namespace saccardi.lonelyboid
{
    public class ParentBehaviour : MonoBehaviour
    {
        internal interface IChild
        {
            public void OnParentChange(ChildBehaviour child, GameObject oldParent, GameObject newParent)
            {
                if (oldParent && oldParent.TryGetComponent<ParentBehaviour>(out var oldComp))
                {
                    oldComp.RemoveChild(child);
                }

                if (newParent && newParent.TryGetComponent<ParentBehaviour>(out var newComp))
                {
                    newComp.AddChild(child);
                }
            }
        }

        protected virtual void AddChild(ChildBehaviour child)
        {
        }

        protected virtual void RemoveChild(ChildBehaviour child)
        {
        }
    }
}