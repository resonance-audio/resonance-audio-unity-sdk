// Copyright 2017 Google Inc. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

/// Resonance Audio material mapper scriptable object that holds the mapping from GUIDs to surface
/// materials (which define the acoustic characteristics of surfaces). The GUID identifies either
/// a Unity Material (which gives a mesh its visual appearance) of a game object or the terrain
/// data of a terrain object.
public class ResonanceAudioMaterialMapper : ScriptableObject {
  // The complete mapping is achieved in two stages, the first stage is from GUID to surface
  // materials, which will be serialized and persisted. The second stage is from GUID to acoustic
  // meshes, which will be gathered on the fly. If the GUID identifies:
  //   - A Unity Material asset, then the acoustic meshes are generated from the game objects
  //     sharing this Unity Material. Because a game object can have multiple Unity Materials,
  //     we also record the sub-mesh index corresponding to each Unity Material.
  //   - A terrain data asset, then the acoustic meshes are the generated from the terrain that
  //     uses this terrain data.
  //
  // Conceptually,
  // The first stage:  GUID --> surface material.
  // The second stage: GUID --> {Unity Material,
  //                             acoustic meshes of game objects using this Unity Material,
  //                             sub-mesh indices of this Unity Material in their game objects}
  //
  //                   GUID --> {terrain data,
  //                             acoustic meshes of terrains using this terrain data}.
  //
  // Thus when the user changes the surface material mapped to a GUID, the system can pass the
  // information down to the acoustic meshes that are affected.

  // Data used to map GUIDs to acoustic meshes of game objects sharing the same Unity Material.
  private class UnityMaterialAcousticMeshData {
    // The Unity Material.
    public Material unityMaterial = null;

    // Acoustic meshes of the game objects that use this Unity Material.
    public List<ResonanceAudioAcousticMesh> acousticMeshes = null;

    // Sub-mesh indices of the game objects that uses this Unity Material.
    public List<int> subMeshIndices = null;

    public UnityMaterialAcousticMeshData() {
      acousticMeshes = new List<ResonanceAudioAcousticMesh>();
      subMeshIndices = new List<int>();
    }
  }

  // Data used to map GUIDs to acoustic meshes of terrains using the same terrain data.
  private class TerrainAcousticMeshData {
    // The terrain data.
    public TerrainData terrainData = null;

    // Acoustic meshes of the terrain objects that share the same |terainData| and will get the
    // same material mapping.
    public List<ResonanceAudioAcousticMesh> acousticMeshes = null;

    public TerrainAcousticMeshData() {
      acousticMeshes = new List<ResonanceAudioAcousticMesh>();
    }
  }

  // Mapping from GUIDs to surface materials. This is the only data that is serialized for this
  // class. All other mappings are "gathered" on the fly.
  [SerializeField]
  private ResonanceAudioRoomManager.SurfaceMaterialDictionary surfaceMaterialFromGuid = null;

  // Mapping from GUIDs to acoustic meshes of game objects sharing the same Unity Material.
  // Gathered on the fly for the currently loaded scenes.
  private Dictionary<string, UnityMaterialAcousticMeshData>
      unityMaterialAcousticMeshDataFromGuid = null;

  // Mapping from GUIDs to acoustic meshes of terrain objects using the same terrain data.
  // Gathered on the fly for the currently loaded scenes.
  private Dictionary<string, TerrainAcousticMeshData> terrainAcousticMeshDataFromGuid = null;

  // Reverb layer mask of the objects included in reverb computation. -1 means all layers.
  [SerializeField]
  private LayerMask reverbLayerMask = -1;

  // Whether to include non-static game objects in reverb computation.
  [SerializeField]
  private bool includeNonStaticGameObjects = true;

  // Default surface material.
  private const ResonanceAudioRoomManager.SurfaceMaterial defaultSurfaceMaterial =
      ResonanceAudioRoomManager.SurfaceMaterial.Transparent;

  /// Initializes the data members.
  public void Initialize() {
    if (surfaceMaterialFromGuid == null) {
      surfaceMaterialFromGuid = new ResonanceAudioRoomManager.SurfaceMaterialDictionary();
    }

    unityMaterialAcousticMeshDataFromGuid = new Dictionary<string, UnityMaterialAcousticMeshData>();
    terrainAcousticMeshDataFromGuid = new Dictionary<string, TerrainAcousticMeshData>();
  }

  /// Applies the material mapping to acoustic meshes generated from mesh renderes and terrains.
  public void ApplyMaterialMapping(MeshRenderer[] meshRenderers,
                                   List<string>[] guidsForMeshRenderers,
                                   Terrain[] activeTerrains, string[] guidsForTerrains,
                                   Shader surfaceMaterialShader) {
    // Build material mapping data.
    BuildUnityMaterialData(meshRenderers, guidsForMeshRenderers, surfaceMaterialShader);
    BuildTerrainData(activeTerrains, guidsForTerrains, surfaceMaterialShader);

    // Apply the material mapping to all of the GUIDs.
    ApplyMaterialMappingToGuids(surfaceMaterialFromGuid.Keys.ToList());

    // Apply object filtering by layers and by the static flags of the game objects.
    ApplyObjectFiltering();
  }

  /// Renders all acoustic meshes that are included.
  public void RenderAcousticMeshes() {
    var acousticMeshes = GetIncludedAcousticMeshes();
    for (int i = 0; i < acousticMeshes.Count; ++i) {
      acousticMeshes[i].Render();
    }
  }

  /// Gets the Unity Material mapped from a GUID. Returns null if a mapping cannot be found in the
  /// currently loaded scenes.
  public Material GetUnityMaterial(string guid) {
    UnityMaterialAcousticMeshData data;
    if (unityMaterialAcousticMeshDataFromGuid.TryGetValue(guid, out data)) {
      return data.unityMaterial;
    } else {
      return null;
    }
  }

  /// Gets the terrain data mapped from a GUID. Returns null if a mapping cannot be found in the
  /// currently loaded scenes.
  public TerrainData GetTerrainData(string guid) {
    TerrainAcousticMeshData data;
    if (terrainAcousticMeshDataFromGuid.TryGetValue(guid, out data)) {
      return data.terrainData;
    } else {
      return null;
    }
  }

  /// Gets all acoustic meshes that should be included in a reverb computation.
  public List<ResonanceAudioAcousticMesh> GetIncludedAcousticMeshes() {
    List <ResonanceAudioAcousticMesh> includedAcousticMeshes =
        new List <ResonanceAudioAcousticMesh>();

    foreach (var unityMaterialAcousticMeshData in unityMaterialAcousticMeshDataFromGuid.Values) {
      for (int i = 0; i < unityMaterialAcousticMeshData.acousticMeshes.Count; ++i) {
        var acousticMesh = unityMaterialAcousticMeshData.acousticMeshes[i];
        if (acousticMesh.IsIncluded()) {
          includedAcousticMeshes.Add(acousticMesh);
        }
      }
    }

    foreach (var terrainAcousticMeshData in terrainAcousticMeshDataFromGuid.Values) {
      for (int i = 0; i < terrainAcousticMeshData.acousticMeshes.Count; ++i) {
        var acousticMesh = terrainAcousticMeshData.acousticMeshes[i];
        if (acousticMesh.IsIncluded()) {
          includedAcousticMeshes.Add(acousticMesh);
        }
      }
    }

    return includedAcousticMeshes;
  }

  // Creates acoustic meshes from game objects and builds a mapping from GUIDs of the Unity
  // Materials used by these game objects to the generated acoustic meshes.
  private void BuildUnityMaterialData(MeshRenderer[] meshRenderers,
                                      List<string>[] guidsForMeshRenderers,
                                      Shader surfaceMaterialShader) {
    unityMaterialAcousticMeshDataFromGuid.Clear();
    for (int meshRendererIndex = 0; meshRendererIndex < meshRenderers.Length; ++meshRendererIndex) {
      var meshRenderer = meshRenderers[meshRendererIndex];
      var gameObject = meshRenderer.gameObject;

      // Skip if the mesh renderer does not have Unity Materials.
      var unityMaterials = meshRenderer.sharedMaterials;
      if (unityMaterials.Length == 0) {
        continue;
      }

      // Exclude inactive game objects.
      if (!gameObject.activeInHierarchy) {
        continue;
      }

      // Generate an acoustic mesh for the game object. Skip if failed.
      var acousticMesh = ResonanceAudioAcousticMesh.GenerateFromMeshFilter(
          gameObject.GetComponent<MeshFilter>(), surfaceMaterialShader);
      if (acousticMesh == null) {
        Debug.LogError("acousticMesh == null");
        continue;
      }

      // Each Unity Material of a mesh renderer correspondes to a sub-mesh.
      var guidsForMeshRenderer = guidsForMeshRenderers[meshRendererIndex];
      for (int subMeshIndex = 0; subMeshIndex < unityMaterials.Length; ++subMeshIndex) {
        // Find the GUID that identifies this Unity Material.
        var unityMaterial = unityMaterials[subMeshIndex];
        string guid = guidsForMeshRenderer[subMeshIndex];

        // If this guid is not mapped to a surface material yet, map it to the default surface
        // material.
        if (!surfaceMaterialFromGuid.ContainsKey(guid)) {
          surfaceMaterialFromGuid.Add(guid, defaultSurfaceMaterial);
        }

        if (!unityMaterialAcousticMeshDataFromGuid.ContainsKey(guid)) {
          unityMaterialAcousticMeshDataFromGuid[guid] = new UnityMaterialAcousticMeshData();
        }
        UnityMaterialAcousticMeshData data = unityMaterialAcousticMeshDataFromGuid[guid];
        data.unityMaterial = unityMaterial;
        data.acousticMeshes.Add(acousticMesh);
        data.subMeshIndices.Add(subMeshIndex);
      }
    }
  }

  // Creates acoustic meshes from terrain objects and builds a mapping from GUIDs of the terrain
  // data to the generated acoustic meshes.
  private void BuildTerrainData(Terrain[] activeTerrains, string[] guidsForTerrains,
                                Shader surfaceMaterialShader) {
    terrainAcousticMeshDataFromGuid.Clear();
    for (int terrainIndex = 0; terrainIndex < activeTerrains.Length; ++terrainIndex) {
      var terrain = activeTerrains[terrainIndex];
      string guid = guidsForTerrains[terrainIndex];

      // Generate an acoustic mesh for the terrain object.
      var acousticMesh = ResonanceAudioAcousticMesh.GenerateFromTerrain(terrain,
                                                                        surfaceMaterialShader);

      // If this guid is not mapped to a surface material yet, map it to the default surface
      // material.
      if (!surfaceMaterialFromGuid.ContainsKey(guid)) {
        surfaceMaterialFromGuid.Add(guid, defaultSurfaceMaterial);
      }

      if (!terrainAcousticMeshDataFromGuid.ContainsKey(guid)) {
        terrainAcousticMeshDataFromGuid[guid] = new TerrainAcousticMeshData();
      }
      TerrainAcousticMeshData data = terrainAcousticMeshDataFromGuid[guid];
      data.terrainData = terrain.terrainData;
      data.acousticMeshes.Add(acousticMesh);
    }
  }

  // Applies the material mapping (stored in |surfaceMaterialFromGuid|) to |guids| and pass the
  // information to the acoustic meshes, of game objects sharing the Unity Material identified
  // by GUID, or of terrain objects using the terrain data identified by GUID.
  private void ApplyMaterialMappingToGuids(List<string> guids) {
    for (int i = 0; i < guids.Count; ++i) {
      var guid = guids[i];
      ResonanceAudioRoomManager.SurfaceMaterial surfaceMaterial = surfaceMaterialFromGuid[guid];
      if (unityMaterialAcousticMeshDataFromGuid.ContainsKey(guid)) {
        ApplySurfaceMaterialToGameObjects(surfaceMaterial, guid);
      } else if (terrainAcousticMeshDataFromGuid.ContainsKey(guid)) {
        ApplySurfaceMaterialToTerrains(surfaceMaterial, guid);
      }
    }
  }

  // Applies surface material to (the acoustic meshes) of the game objects whose Unity Materials
  // are identified by |guid|.
  private void ApplySurfaceMaterialToGameObjects(
      ResonanceAudioRoomManager.SurfaceMaterial surfaceMaterial, string guid) {
    UnityMaterialAcousticMeshData acosuticMeshesData = unityMaterialAcousticMeshDataFromGuid[guid];
    if (acosuticMeshesData.acousticMeshes.Count != acosuticMeshesData.subMeshIndices.Count) {
      Debug.LogError("Number of acoustic meshes (" + acosuticMeshesData.acousticMeshes.Count +
                     ") != number of sub-mesh indices (" +
                     acosuticMeshesData.subMeshIndices.Count + ")");
    }

    var acousticMeshes = acosuticMeshesData.acousticMeshes;
    var subMeshIndices = acosuticMeshesData.subMeshIndices;
    for (int i = 0; i < acousticMeshes.Count; ++i) {
      acousticMeshes[i].SetSurfaceMaterialToSubMesh(surfaceMaterial, subMeshIndices[i]);
    }
  }

  // Applies surface material to (the acoustic meshes) of terrains whose terrain data are
  // identified by |guid|.
  private void ApplySurfaceMaterialToTerrains(
      ResonanceAudioRoomManager.SurfaceMaterial surfaceMaterial, string guid) {
    TerrainAcousticMeshData acousticMeshesData = terrainAcousticMeshDataFromGuid[guid];
    var acousticMeshes = acousticMeshesData.acousticMeshes.ToList();
    for (int i = 0; i < acousticMeshes.Count; ++i) {
      acousticMeshes[i].SetSurfaceMaterialToAllSubMeshes(surfaceMaterial);
    }
  }

  // Applies the object filtering to acoustic meshes.
  private void ApplyObjectFiltering() {
    var unityMaterialAcosuticMeshesDataList = unityMaterialAcousticMeshDataFromGuid.Values.ToList();
    for (int i = 0; i < unityMaterialAcosuticMeshesDataList.Count; ++i) {
      var acousticMeshesData = unityMaterialAcosuticMeshesDataList[i];
      for (int j = 0; j < acousticMeshesData.acousticMeshes.Count; ++j) {
        var acousticMesh = acousticMeshesData.acousticMeshes[j];
        acousticMesh.isIncludedByObjectFiltering =
            IsIncludedByObjectFiltering(acousticMesh.sourceObject);
      }
    }

    var terrainAcousticMeshDataList = terrainAcousticMeshDataFromGuid.Values.ToList();
    for (int i = 0; i < terrainAcousticMeshDataList.Count; ++i) {
      var terrainAcousticMeshData = terrainAcousticMeshDataList[i];

      for (int j = 0; j < terrainAcousticMeshData.acousticMeshes.Count; ++j) {
        var acousticMesh = terrainAcousticMeshData.acousticMeshes[j];
        acousticMesh.isIncludedByObjectFiltering =
            IsIncludedByObjectFiltering(acousticMesh.sourceObject);
      }
    }
  }

  // Determines whether the game object is included by the object filtering. Currenlty there are
  // two kinds of filtering: by layers and by the static flag of the game object.
  private bool IsIncludedByObjectFiltering(GameObject gameObject) {
    // Filtering by layers.
    var gameObjectLayerMask = 1 << gameObject.layer;
    if ((gameObjectLayerMask & reverbLayerMask.value) == 0) {
      return false;
    }

    // Filtering by the static flag.
    return includeNonStaticGameObjects || gameObject.isStatic;
  }
}
