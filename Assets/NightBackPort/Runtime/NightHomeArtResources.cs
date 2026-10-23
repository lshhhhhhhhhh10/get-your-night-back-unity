// @role Owns and cleans up generated household meshes

using System.Collections.Generic;
using UnityEngine;
namespace NightBackPort
{
    public sealed class NightHomeArtResources : MonoBehaviour
    {
        // Only combined, instance-specific meshes are owned here. Palette and primitive templates are shared.
        [System.NonSerialized] public List<Mesh> meshes=new List<Mesh>();
        void OnDestroy(){foreach(var mesh in meshes){if(!mesh)continue;
#if UNITY_EDITOR
            if(UnityEditor.AssetDatabase.Contains(mesh))continue;
#endif
            if(Application.isPlaying)Destroy(mesh);else DestroyImmediate(mesh);
        }}
    }
}
