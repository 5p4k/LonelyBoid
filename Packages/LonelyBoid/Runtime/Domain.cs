using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.TerrainTools;
using UnityEngine;
using UnityEngine.Assertions;

namespace saccardi.lonelyboid
{
    public enum DomainObjectType
    {
        Flock,
        Force
    }

    public class Domain : MonoBehaviour
    {
    }

    [CustomEditor(typeof(Domain))]
    public class DomainEditor : Editor
    {
        
    }
}