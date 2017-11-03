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

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections.Generic;

/// An editor window that provides UI for reverb baking related tasks:
/// 1. Select reverb probes and bake reverb to them.
/// 2. Modify the material mappings.
public class ResonanceAudioReverbBakingWindow : EditorWindow {
  private SerializedProperty reverbLayerMask = null;
  private SerializedProperty includeNonStaticGameObjects = null;
  private SerializedProperty materialMappingGuids = null;
  private SerializedProperty materialMappingSurfaceMaterials = null;

  private GUIContent reverbProbesBakingLabel = new GUIContent("Bake Reverb To Probes",
      "Reverb probe selections for baking.");
  private GUIContent selectAllProbesLabel = new GUIContent("Select All",
      "Selects all reverb probes.");
  private GUIContent clearAllProbesLabel = new GUIContent("Clear",
      "Clears reverb probe selections.");
  private GUIContent bakeLabel = new GUIContent("Bake", "Bake reverb to selected reverb probes.");
  private GUIContent materialMappingLabel = new GUIContent("Map Materials",
      "Maps Unity Materials or Terrains to surface materials.");
  private GUIContent visualizeModeLabel = new GUIContent("Visualize Mode",
      "Toggle to visualize the material mapping in the Scene View.");
  private GUIContent reverbLayerMaskLabel = new GUIContent("Reverb Mask",
      "Which layers of game objects are included in reverb computation.");
  private GUIContent nonStaticGameObjectLabel = new GUIContent("Include Non-Static Game Objects",
      "Should non-static game objects be included in reverb computation?");
  private GUIContent clearAllMappingLabel = new GUIContent("Reset All",
      "Resets the material mapping selections to default.");

  // Whether to visualize the material mapping.
  private bool isInVisualizeMode = false;

  // The material mapper instance.
  private ResonanceAudioMaterialMapper materialMapper = null;

  // The material mapper updater instance.
  private ResonanceAudioMaterialMapperUpdater materialMapperUpdater = null;

  // The serialized object of the material mapper.
  private SerializedObject serializedMaterialMapper = null;

  // Whether the scene view needs to be redrawn. True when some things are changed (e.g. material
  // mappings changed or objects moved).
  private bool redraw = false;

  // The set of scene views whose shaders have been updated. This is used to make sure that each
  // scene view is at least updated once after OnEnable() (during OnEnable() the scene views might
  // not be available yet).
  private HashSet <int> updatedSceneViews = null;

  // The thumbnail previews of the surface materials in the inspector window, shown as solid color
  // patches.
  private Texture2D[] surfaceMaterialPreviews = null;

  // Shader to visualize surface materials.
  private Shader surfaceMaterialShader = null;

  private const int guidLabelWidth = 150;
  private const int materialRowMargin = 5;
  private const int materialPreviewSize = 50;
  private GUILayoutOption previewHeight = null;
  private GUILayoutOption previewWidth = null;
  private GUIStyle materialRowStyle = null;

  // The scroll position.
  private Vector2 scrollPosition = new Vector2();

  // The foldouts of the reverb baking and material mapping sections.
  private bool showReverbBaking = true;
  private bool showMaterialMapping = true;

  // The path to the material mapper asset.
  private const string materialMapperAssetPath =
      "Assets/ResonanceAudio/Resources/ResonanceAudioMaterialMapper.asset";

  // Color coding for surface materials used to visualize the surface material assignment in the
  // scene as well as in the material picking UI.
  // The following colors are generated using http://vrl.cs.brown.edu/color, while setting a
  // starting point of "rgb(128,128,128)", and maximizing the "Perceptual Distance" and
  // "Pair Preference" parameters. This color mapping is shared by the shader and the preview
  // thumbnails of the surface materials (see InitializeColorArrayInShader() and
  // InitializeSurfaceMaterialPreviews()).
  private static readonly Color[] surfaceMaterialColors = new Color[] {
      new Color(0.500000f, 0.500000f, 0.500000f),
      new Color(0.545098f, 0.909804f, 0.678431f),
      new Color(0.184314f, 0.258824f, 0.521569f),
      new Color(0.552941f, 0.737255f, 0.976471f),
      new Color(0.035294f, 0.376471f, 0.074510f),
      new Color(0.952941f, 0.415686f, 0.835294f),
      new Color(0.105882f, 0.894118f, 0.427451f),
      new Color(0.541176f, 0.015686f, 0.345098f),
      new Color(0.631373f, 0.847059f, 0.196078f),
      new Color(0.513725f, 0.003922f, 0.741176f),
      new Color(0.949020f, 0.690196f, 0.964706f),
      new Color(0.082353f, 0.305882f, 0.337255f),
      new Color(0.152941f, 0.792157f, 0.901961f),
      new Color(0.921569f, 0.070588f, 0.254902f),
      new Color(0.274510f, 0.635294f, 0.423529f),
      new Color(0.556863f, 0.215686f, 0.066667f),
      new Color(0.960784f, 0.803922f, 0.686275f),
      new Color(0.305882f, 0.282353f, 0.035294f),
      new Color(0.917647f, 0.839216f, 0.141176f),
      new Color(0.521569f, 0.458824f, 0.858824f),
      new Color(0.937255f, 0.592157f, 0.176471f),
      new Color(0.980392f, 0.105882f, 0.988235f),
      new Color(0.725490f, 0.423529f, 0.552941f)
  };

  [MenuItem("ResonanceAudio/Reverb Baking")]
  private static void Initialize() {
    ResonanceAudioReverbBakingWindow window =
        EditorWindow.GetWindow<ResonanceAudioReverbBakingWindow>();
    window.Show();
  }

  void OnEnable() {
    updatedSceneViews = new HashSet<int>();

    InitializeColorArrayInShader();
    InitializeGuiParameters();
    InitializeSurfaceMaterialPreviews();
    InitializeSurfaceMaterialShader();

    LoadOrCreateMaterialMapper();
    LoadOrCreateMaterialMapperUpdater();
    InitializeMaterialMapperAndLoadProperties();

    isInVisualizeMode = false;

    EditorSceneManager.sceneOpened += (Scene scene, OpenSceneMode mode) => OnSceneOrModeSwitch();
    EditorSceneManager.sceneClosed += (Scene scene) => OnSceneOrModeSwitch();
    EditorApplication.playmodeStateChanged = OnSceneOrModeSwitch;
    SceneView.onSceneGUIDelegate += OnSceneGUI;
  }

  void OnDisable() {
    EditorSceneManager.sceneOpened -= (Scene scene, OpenSceneMode mode) => OnSceneOrModeSwitch();
    EditorSceneManager.sceneClosed -= (Scene scene) => OnSceneOrModeSwitch();
    EditorApplication.playmodeStateChanged = null;
    SceneView.onSceneGUIDelegate -= OnSceneGUI;

    // Destroy the material mapper updater if not null.
    if (!EditorApplication.isPlaying && materialMapperUpdater != null) {
      DestroyImmediate(materialMapperUpdater.gameObject);
    }

    if (isInVisualizeMode) {
      isInVisualizeMode = false;
      RefreshMaterialMapper();
      UpdateShader();
    }
  }

  /// @cond
  void OnGUI() {
    serializedMaterialMapper.Update();

    EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);
    showReverbBaking = EditorGUILayout.Foldout(showReverbBaking, reverbProbesBakingLabel);
    if (showReverbBaking) {
      ++EditorGUI.indentLevel;
      DrawProbeSelection();

      EditorGUILayout.Separator();

      DrawBakeButton();
      --EditorGUI.indentLevel;
    }

    EditorGUILayout.Separator();

    showMaterialMapping = EditorGUILayout.Foldout(showMaterialMapping, materialMappingLabel);
    if (showMaterialMapping) {
      ++EditorGUI.indentLevel;
      DrawVisualizeModeCheckbox();

      EditorGUILayout.Separator();

      DrawObjectFiltering();

      EditorGUILayout.Separator();

      DrawMaterialMappingGUI();
      --EditorGUI.indentLevel;
    }
    EditorGUI.EndDisabledGroup();

    serializedMaterialMapper.ApplyModifiedProperties();
  }
  /// @endcond

  // Loads the material mapper asset; creates one if not found.
  private void LoadOrCreateMaterialMapper() {
    materialMapper = AssetDatabase.LoadAssetAtPath<ResonanceAudioMaterialMapper>(
        materialMapperAssetPath);
    if (materialMapper == null) {
      materialMapper = ScriptableObject.CreateInstance<ResonanceAudioMaterialMapper>();
      AssetDatabase.CreateAsset(materialMapper, materialMapperAssetPath);
      AssetDatabase.SaveAssets();
    }

    serializedMaterialMapper = new UnityEditor.SerializedObject(materialMapper);
  }

  // Loads the unique material mapper updater; creates one if not found.
  private void LoadOrCreateMaterialMapperUpdater() {
    if (EditorApplication.isPlayingOrWillChangePlaymode) {
      return;
    }

    var scene = EditorSceneManager.GetActiveScene();
    GameObject[] rootGameObjects = scene.GetRootGameObjects();
    for (int i = 0; i < rootGameObjects.Length; ++i) {
      var foundUpdater =
          rootGameObjects[i].GetComponentInChildren<ResonanceAudioMaterialMapperUpdater>();
      if (foundUpdater != null) {
        ResetMaterialMapperUpdater(foundUpdater);
        return;
      }
    }

    // Create an empty GameObject at the root, which is hidden and not saved, to hold a
    // ResonanceAudioMaterialMapperUpdater.
    GameObject updaterObject = new GameObject("Holder of mapper updater ID = ");
    updaterObject.hideFlags = HideFlags.HideAndDontSave;
    var newUpdater = updaterObject.AddComponent<ResonanceAudioMaterialMapperUpdater>();
    updaterObject.name += newUpdater.GetInstanceID();
    ResetMaterialMapperUpdater(newUpdater);
  }

  // Resets the |materialMapperUpdater| to |newUpdater| and destroy the old one if necessary.
  private void ResetMaterialMapperUpdater(ResonanceAudioMaterialMapperUpdater newUpdater) {
    if (newUpdater != materialMapperUpdater) {
      if (materialMapperUpdater != null) {
        DestroyImmediate(materialMapperUpdater.gameObject);
      }

      materialMapperUpdater = newUpdater;
    }
    materialMapperUpdater.RefreshMaterialMapper = RefreshMaterialMapper;
  }

  // Initializes the material mapper and loads properties to be displayed in this window.
  private void InitializeMaterialMapperAndLoadProperties() {
    materialMapper.Initialize();
    RefreshMaterialMapper();
    UpdateShader();

    reverbLayerMask = serializedMaterialMapper.FindProperty("reverbLayerMask");
    includeNonStaticGameObjects =
        serializedMaterialMapper.FindProperty("includeNonStaticGameObjects");
    var surfaceMaterialFromGuid = serializedMaterialMapper.FindProperty("surfaceMaterialFromGuid");
    materialMappingGuids = surfaceMaterialFromGuid.FindPropertyRelative("guids");
    materialMappingSurfaceMaterials =
        surfaceMaterialFromGuid.FindPropertyRelative("surfaceMaterials");
  }

  // Initializes the surface material colors in a global vector array for shaders.
  private void InitializeColorArrayInShader() {
    var numSurfaceMaterials =
        Enum.GetValues(typeof(ResonanceAudioRoomManager.SurfaceMaterial)).Length;
    Vector4[] vectorArray = new Vector4[numSurfaceMaterials];
    for (int surfaceMaterialIndex = 0; surfaceMaterialIndex < numSurfaceMaterials;
         ++surfaceMaterialIndex) {
      var color = surfaceMaterialColors[surfaceMaterialIndex];
      vectorArray[surfaceMaterialIndex] = new Vector4(color.r, color.g, color.b, 0.5f);
    }

    Shader.SetGlobalVectorArray("_SurfaceMaterialColors", vectorArray);
  }

  // Initializes various GUI parameters.
  private void InitializeGuiParameters() {
    previewHeight = GUILayout.Height((float) materialPreviewSize);
    previewWidth = GUILayout.Width((float) materialPreviewSize);
    materialRowStyle = new GUIStyle();
    materialRowStyle.margin = new RectOffset(materialRowMargin, materialRowMargin,
                                             materialRowMargin, materialRowMargin);
  }

  // Initializes the thumbnail previews used in the material picking UI. Each surface material
  // is shown as a square filled with solid color.
  private void InitializeSurfaceMaterialPreviews () {
    int numSurfaceMaterials = surfaceMaterialColors.Length;
    surfaceMaterialPreviews = new Texture2D[numSurfaceMaterials];
    for (int surfaceMaterialIndex = 0; surfaceMaterialIndex < numSurfaceMaterials;
         ++surfaceMaterialIndex) {
      var color = surfaceMaterialColors[surfaceMaterialIndex];
      Texture2D surfaceMaterialPreview = new Texture2D(materialPreviewSize, materialPreviewSize);
      var pixelArraySize = surfaceMaterialPreview.GetPixels().Length;
      Color[] pixelArray = new Color[pixelArraySize];
      for (int pixelArrayIndex = 0; pixelArrayIndex < pixelArraySize; ++pixelArrayIndex) {
        pixelArray[pixelArrayIndex] = color;
      }
      surfaceMaterialPreview.SetPixels(pixelArray);
      surfaceMaterialPreview.Apply();
      surfaceMaterialPreviews[surfaceMaterialIndex] = surfaceMaterialPreview;
    }
  }

  // Initializes the surface material shader which visualizes surface materials as colors.
  private void InitializeSurfaceMaterialShader() {
    surfaceMaterialShader = Shader.Find("ResonanceAudio/SurfaceMaterial");
    if (surfaceMaterialShader == null) {
      Debug.LogError("Surface material shader not found");
      return;
    }
  }

  // Refreshes the material mapper's data to reflect external changes (e.g. scene modified,
  // material mapping changed).
  private void RefreshMaterialMapper() {
    if (EditorApplication.isPlaying) {
      return;
    }

    MeshRenderer[] meshRenderers = null;
    List<string>[] guidsForMeshRenderers = null;
    GatherMeshRenderersAndGuids(ref meshRenderers, ref guidsForMeshRenderers);

    Terrain[] activeTerrains = null;
    string[] guidsForTerrains = null;
    GatherTerrainsAndGuids(ref activeTerrains, ref guidsForTerrains);
    materialMapper.ApplyMaterialMapping(meshRenderers, guidsForMeshRenderers, activeTerrains,
                                        guidsForTerrains, surfaceMaterialShader);
    redraw = true;
  }

  // Gathers the mesh renderes of game objects, and the GUIDs of the Unity Materials of
  // each sub-mesh.
  private void GatherMeshRenderersAndGuids(ref MeshRenderer[] meshRenderers,
                                           ref List<string>[] guidsForMeshRenderers) {
    List<MeshRenderer> meshRenderersList = new List<MeshRenderer>();
    List<List<string>> guidsForMeshRenderersList = new List<List<string>>();

    // Gather mesh renderers from all scenes.
    for (int sceneIndex = 0; sceneIndex < EditorSceneManager.sceneCount; ++sceneIndex) {
      Scene scene = EditorSceneManager.GetSceneAt(sceneIndex);
      if (!scene.isLoaded) {
        continue;
      }

      // Get the root game objects in this loaded scene.
      GameObject[] rootGameObjects = scene.GetRootGameObjects();
      for (int rootGameObjectIndex = 0; rootGameObjectIndex < rootGameObjects.Length;
           ++rootGameObjectIndex) {
        var rootGameObject = rootGameObjects[rootGameObjectIndex];

        var meshRenderersInChildren = rootGameObject.GetComponentsInChildren<MeshRenderer>();
        for (int meshRenderIndex = 0; meshRenderIndex < meshRenderersInChildren.Length;
             ++meshRenderIndex) {
          var meshRenderer = meshRenderersInChildren[meshRenderIndex];
          meshRenderersList.Add(meshRenderer);

          // Each Unity Material of a mesh renderer correspondes to a sub-mesh.
          var unityMaterials = meshRenderer.sharedMaterials;
          var guidsForMeshRenderer = new List<string>();
          for (int subMeshIndex = 0; subMeshIndex < unityMaterials.Length; ++subMeshIndex) {
            // Find the GUID that identifies this Unity Material.
            var unityMaterial = unityMaterials[subMeshIndex];
            string assetPath = AssetDatabase.GetAssetPath(unityMaterial);
            guidsForMeshRenderer.Add(AssetDatabase.AssetPathToGUID(assetPath));
          }
          guidsForMeshRenderersList.Add(guidsForMeshRenderer);
        }
      }
    }

    meshRenderers = meshRenderersList.ToArray();
    guidsForMeshRenderers = guidsForMeshRenderersList.ToArray();
  }

  // Gathers the terrains and the GUIDs of the terrain data.
  private void GatherTerrainsAndGuids(ref Terrain[] activeTerrains, ref string[] guidsForTerrains) {
    List<string> guidsForTerrainsList = new List<string>();

    // Gather from |activeTerrains|, the terrains in all loaded scenes.
    activeTerrains = Terrain.activeTerrains;
    foreach (var terrain in activeTerrains) {
      // Finds the GUID that identifies this terrain data.
      string assetPath = AssetDatabase.GetAssetPath(terrain.terrainData);
      guidsForTerrainsList.Add(AssetDatabase.AssetPathToGUID(assetPath));
    }
    guidsForTerrains = guidsForTerrainsList.ToArray();
  }

  // Attempts to update the scene views' shader, using the surface material shader stored in
  // |materialMapper| if |isInVisualizeMode| is true, and using the default shader otherwise.
  // Defers the updating to OnSceneGUI() if the scene views are not ready yet.
  private void UpdateShader() {
    var sceneViews = SceneView.sceneViews;

    // Defer the updating if the scene views are not ready.
    if (sceneViews.Count == 0) {
      updatedSceneViews.Clear();
      return;
    }

    // Update all ready scene views.
    for (int i = 0; i < sceneViews.Count; ++i) {
      UpdateShaderForSceneView((SceneView) sceneViews[i]);
    }
  }

  // Updates the shader of a specific scene view.
  private void UpdateShaderForSceneView(SceneView sceneView) {
    if (isInVisualizeMode) {
      sceneView.SetSceneViewShaderReplace(surfaceMaterialShader, "RenderType");
    } else {
      sceneView.SetSceneViewShaderReplace(null, null);
    }
    sceneView.Repaint();
    updatedSceneViews.Add(sceneView.GetInstanceID());
  }

  // The UI for selecting a subset of reverb probes to bake reverb to.
  private void DrawProbeSelection() {
    ResonanceAudioReverbProbe[] allReverbProbes =
        UnityEngine.Object.FindObjectsOfType<ResonanceAudioReverbProbe>();

    // Clean up the deleted reverb probes.
    var selectedReverbProbes = ResonanceAudioReverbComputer.selectedReverbProbes;
    selectedReverbProbes.RemoveAll(reverbProbe => reverbProbe == null);

    for (int i = 0; i < allReverbProbes.Length; ++i) {
      var reverbProbe = allReverbProbes[i];
      bool currentlySelected = selectedReverbProbes.Contains(reverbProbe);
      if (EditorGUILayout.ToggleLeft(reverbProbe.name, currentlySelected)) {
        if (!currentlySelected) {
          // Reverb probe selected.
          selectedReverbProbes.Add(reverbProbe);
        }
      } else {
        if (currentlySelected) {
          // Reverb probe de-selected.
          selectedReverbProbes.Remove(reverbProbe);
        }
      }
    }
    if (allReverbProbes.Length > 0) {
      EditorGUILayout.Separator();

      EditorGUILayout.BeginHorizontal();
      GUILayout.Space(15 * EditorGUI.indentLevel);
      if (GUILayout.Button(selectAllProbesLabel)) {
        for (int i = 0; i < allReverbProbes.Length; ++i) {
          if (!selectedReverbProbes.Contains(allReverbProbes[i])) {
            selectedReverbProbes.Add(allReverbProbes[i]);
          }
        }
      }
      if (GUILayout.Button(clearAllProbesLabel)) {
        selectedReverbProbes.Clear();
      }
      EditorGUILayout.EndHorizontal();
    } else {
      EditorGUILayout.HelpBox("No ResonanceAudioReverbProbe exists in the scene.",
                              MessageType.Warning);
    }
  }

  // The UI to compute reverb and bake the results to the selected probes.
  private void DrawBakeButton() {
    // Only enable the "Bake" button when at least one reverb probe is selected and the scene
    // is loaded.
    var scene = EditorSceneManager.GetActiveScene();
    EditorGUI.BeginDisabledGroup(ResonanceAudioReverbComputer.selectedReverbProbes.Count == 0 ||
                                 !scene.isLoaded);
    EditorGUILayout.BeginHorizontal();
    GUILayout.Space(15 * EditorGUI.indentLevel);
    if (GUILayout.Button(bakeLabel)) {
      // We allow only one material mapper in the scene. Find the unique one and ask for acoustic
      // meshes that should be included in the reverb computation.
      if (materialMapper != null) {
        // Compute the reverb for the selected reverb probes using the included acoustic meshes.
        RefreshMaterialMapper();
        ResonanceAudioReverbComputer.ComputeReverb(materialMapper.GetIncludedAcousticMeshes());
      }
    }
    EditorGUILayout.EndHorizontal();
    EditorGUI.EndDisabledGroup();
  }

  // Draws the "Visualize Mode" checkbox.
  private void DrawVisualizeModeCheckbox() {
    if (isInVisualizeMode != EditorGUILayout.Toggle(visualizeModeLabel, isInVisualizeMode)) {
      isInVisualizeMode = !isInVisualizeMode;
      RefreshMaterialMapper();
      UpdateShader();
    }
  }

  // Draws the objects filtering GUI. Users can decide which layers to include, and whether to
  // include non-static objects.
  private void DrawObjectFiltering() {
    EditorGUILayout.PropertyField(reverbLayerMask, reverbLayerMaskLabel);

    EditorGUILayout.Separator();

    EditorGUILayout.PropertyField(includeNonStaticGameObjects, nonStaticGameObjectLabel);
  }

  // Draws the material mapping GUI. The GUI is organized as rows, each row having a GUID (a Unity
  // Material or a terrain data) on the left, and the mapped surface materials on the right.
  private void DrawMaterialMappingGUI() {
    // Show the material mapping as rows.
    scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(false));
    // Access the SurfaceMaterialDictionary's underlying two serialized lists directly.
    for (int i = 0; i < materialMappingGuids.arraySize; ++i) {
      EditorGUILayout.BeginHorizontal(materialRowStyle);
      GUILayout.Space(15 * EditorGUI.indentLevel);
      if (DrawGuidColumn(materialMappingGuids.GetArrayElementAtIndex(i).stringValue)) {
        DrawSurfaceMaterialColumn(materialMappingSurfaceMaterials.GetArrayElementAtIndex(i));
      }
      EditorGUILayout.EndHorizontal();
    }
    EditorGUILayout.EndScrollView();

    EditorGUILayout.Separator();

    DrawClearAllButton();
  }

  // Draws the GUID column: the thumbnail preview first, followed by the name. Depending on whether
  // the GUID identifies a Unity Material or a terrain data, shows the previews and names
  // differently.
  // Returns false if the GUID does not correspond to a Unity Material or a terrain data (maybe
  // not present in the loaded scenes).
  private bool DrawGuidColumn(string guid) {
    // Select the preview and the name.
    Texture2D assetPreview = null;
    string guidName = null;
    var unityMaterial = materialMapper.GetUnityMaterial(guid);
    var terrainData = materialMapper.GetTerrainData(guid);
    if (unityMaterial != null) {
      assetPreview = AssetPreview.GetAssetPreview(unityMaterial);
      guidName = unityMaterial.name;
    } else if (terrainData != null) {
      assetPreview = AssetPreview.GetMiniThumbnail(terrainData);
      guidName = terrainData.name;
    } else {
      // Both |unityMaterial| and |terrainData| are NULL; display nothing for this GUID.
      return false;
    }

    // Draw the preview.
    GUILayout.Box(assetPreview, GUIStyle.none, previewHeight, previewWidth);

    // Display the name.
    if (guidName != null) {
      EditorGUILayout.LabelField(guidName, GUILayout.Width(guidLabelWidth));
    }
    return true;
  }

  // Draws the surface material column: the thumbnail preview first, followed by a drop-down menu
  // to let users choose the mapped material.
  private void DrawSurfaceMaterialColumn(SerializedProperty surfaceMaterialProperty) {
    // Draw the preview.
    var preview = surfaceMaterialPreviews[surfaceMaterialProperty.enumValueIndex];
    GUILayout.Box(preview, GUIStyle.none, previewHeight, previewWidth);

    // Draw the drop-down menu.
    EditorGUILayout.PropertyField(surfaceMaterialProperty, GUIContent.none);
  }

  // Draws the "Clear All" button and clears the material mapping (by clearing the underlying
  // serialized lists).
  private void DrawClearAllButton() {
    EditorGUILayout.BeginHorizontal();
    GUILayout.Space(15 * EditorGUI.indentLevel);
    if (GUILayout.Button(clearAllMappingLabel)) {
      materialMappingGuids.ClearArray();
      materialMappingSurfaceMaterials.ClearArray();
    }
    EditorGUILayout.EndHorizontal();
  }

  private void OnSceneGUI(SceneView sceneView) {
    // Deferred update of the scene view if it is not updated yet.
    if (!updatedSceneViews.Contains(sceneView.GetInstanceID())) {
      UpdateShaderForSceneView(sceneView);
    }

    if (isInVisualizeMode && redraw) {
      materialMapper.RenderAcousticMeshes();
      redraw = false;
    }
  }

  private void OnSceneOrModeSwitch() {
    LoadOrCreateMaterialMapperUpdater();

    // Switching scenes play modes destroys the color-coding Texture2D. Re-initialize them.
    InitializeSurfaceMaterialPreviews();

    // Force repaint this window to reflect the scene changes, which may have a different set of
    // Unity Materials and Terrain data.
    Repaint();
  }
}
