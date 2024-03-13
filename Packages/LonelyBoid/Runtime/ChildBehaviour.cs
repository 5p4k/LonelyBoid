using System;
using UnityEngine;

namespace saccardi.lonelyboid
{
    public class ChildBehaviour : MonoBehaviour, ParentBehaviour.IChild
    {
        [NonSerialized] private GameObject _oldParent; // Used only to detect changes in the editor
        [SerializeField] private GameObject parent;

        protected virtual void OnParentChange(GameObject oldParent, GameObject newParent)
        {
            ((ParentBehaviour.IChild)this).OnParentChange(this, oldParent, newParent);
        }

        private void DetectParentChangeInEditor()
        {
            if (_oldParent == parent) return;
            OnParentChange(_oldParent, parent);
            _oldParent = parent;
        }

        public GameObject Parent
        {
            get => parent;
            set
            {
                if (parent == value) return;
                OnParentChange(parent, value);
                parent = value;
                _oldParent = value;
            }
        }

        private void Start()
        {
            DetectParentChangeInEditor();
        }

        private void OnValidate()
        {
            DetectParentChangeInEditor();
        }
    }
}