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
using UnityEngine;
using System.Collections;

/// A custom editor for properties on the ResonanceAudioReverbProbe script. This appears in the
/// Inspector window of a ResonanceAudioReverbProbe object.
[CustomEditor(typeof(ResonanceAudioReverbProbe))]
public class ResonanceAudioReverbProbeEditor : Editor {
  private SerializedProperty runtimeApplicationRegionShape = null;
  private SerializedProperty boxApplicationRegionSize = null;
  private SerializedProperty onlyApplyWhenVisible = null;
  private SerializedProperty reverbGainDb = null;
  private SerializedProperty reverbBrightness = null;
  private SerializedProperty reverbTime = null;
  private SerializedProperty rt60s = null;
  private SerializedProperty sphereApplicationRegionRadius = null;

  private GUIContent applicationRegionShapeLabel = new GUIContent("Shape",
      "Shape of the region of application of this reverb.");
  private GUIContent onlyApplyWhenVisibleLabel = new GUIContent("Only When Visible",
      "Applies this reverb only when the center of the probe is visible from the listener. The " +
      "visibility check will be done with respect to the ResonanceAudioListener.occlusionMask " +
      "selection.");
  private GUIContent radiusLabel = new GUIContent("Radius",
      "Sets the radius of a spherical region of application.");
  private GUIContent reverbGainLabel = new GUIContent("Gain (dB)",
      "Applies a gain adjustment to the reverberation in the room. The default value will leave " +
      "reverb unaffected.");
  private GUIContent reverbPropertiesLabel = new GUIContent("Reverb Properties",
      "Parameters to adjust the reverb properties of the room.");
  private GUIContent reverbBrightnessLabel = new GUIContent("Brightness",
      "Adjusts the balance between high and low frequencies in the reverb.");
  private GUIContent reverbTimeLabel = new GUIContent("Time",
      "Adjusts the overall duration of the reverb by a positive scaling factor.");
  private GUIContent rt60sLabel = new GUIContent("RT60s for frequency bands (sec)",
      "RT60: Time required for reverb to decay 60 dB");
  private GUIContent sizeLabel = new GUIContent("Size",
      "Sets the dimensions of a box-shaped region of application.");

  // Various parameters for the visualization of rt60 values.
  private Color barBackgroundColor = new Color(65.0f / 255.0f, 65.0f / 255.0f, 65.0f / 255.0f);
  private Color barColor = new Color(186.0f / 255.0f, 117.0f / 255.0f, 33.0f / 255.0f);
  private const float maxBarHeight = 100.0f;
  private const float maxRt60 = 3.0f;
  private const int rt60ValueFieldMarginLeft = 0;
  private const int rt60ValueFieldMarginRight = 5;
  private const int rt60ValueFieldMinWidth = 20;

  void OnEnable () {
    runtimeApplicationRegionShape = serializedObject.FindProperty("runtimeApplicationRegionShape");
    boxApplicationRegionSize = serializedObject.FindProperty("boxApplicationRegionSize");
    onlyApplyWhenVisible = serializedObject.FindProperty("onlyApplyWhenVisible");
    reverbGainDb = serializedObject.FindProperty("reverbGainDb");
    reverbBrightness = serializedObject.FindProperty("reverbBrightness");
    reverbTime = serializedObject.FindProperty("reverbTime");
    sphereApplicationRegionRadius = serializedObject.FindProperty("sphereApplicationRegionRadius");
    rt60s = serializedObject.FindProperty("rt60s");
  }

  /// @cond
  public override void OnInspectorGUI () {
    serializedObject.Update();

    DrawApplicationRegion();

    EditorGUILayout.Separator();

    DrawRT60s(rt60s);

    EditorGUILayout.Separator();

    DrawReverbModifiers();

    serializedObject.ApplyModifiedProperties();
  }
  /// @endcond

  // Show the parameters of the region of application.
  private void DrawApplicationRegion() {
    EditorGUILayout.PropertyField(runtimeApplicationRegionShape, applicationRegionShapeLabel);
    switch ((ResonanceAudioReverbProbe.ApplicationRegionShape)
            runtimeApplicationRegionShape.enumValueIndex) {
      case ResonanceAudioReverbProbe.ApplicationRegionShape.Sphere:
        sphereApplicationRegionRadius.floatValue =
            EditorGUILayout.FloatField(radiusLabel, sphereApplicationRegionRadius.floatValue);
        break;
      case ResonanceAudioReverbProbe.ApplicationRegionShape.Box:
        EditorGUILayout.PropertyField(boxApplicationRegionSize, sizeLabel);
        break;
    }
    EditorGUILayout.PropertyField(onlyApplyWhenVisible, onlyApplyWhenVisibleLabel);
  }

  // Draw the rt60s as vertical bar graphs, with values displayed at the bottom.
  private void DrawRT60s(SerializedProperty rt60s) {
    EditorGUILayout.LabelField(rt60sLabel);

    // Reserve an area for all the bar graphs.
    Rect barAreaRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none,
                                                GUILayout.Height(maxBarHeight),
                                                GUILayout.ExpandWidth(true));
    EditorGUI.DrawRect(barAreaRect, barBackgroundColor);

    // Show the rt60 values as text fields at the bottom.
    GUIStyle textFieldStyle = GUIStyle.none;
    textFieldStyle.normal.textColor = GUI.skin.textField.normal.textColor;
    textFieldStyle.alignment = TextAnchor.UpperCenter;
    textFieldStyle.margin.left = rt60ValueFieldMarginLeft;
    textFieldStyle.margin.right = rt60ValueFieldMarginRight;

    EditorGUILayout.BeginHorizontal();
    Rect[] rt60ValueTextRects = new Rect[rt60s.arraySize];
    EditorGUILayout.Space();
    for (int i = 0; i < rt60s.arraySize; ++i) {
      string rt60ValueText = rt60s.GetArrayElementAtIndex(i).floatValue.ToString("F2");
      EditorGUILayout.TextField(rt60ValueText, textFieldStyle,
                                GUILayout.MinWidth(rt60ValueFieldMinWidth));
      Rect lastRect = GUILayoutUtility.GetLastRect();
      rt60ValueTextRects[i] = lastRect;
    }
    EditorGUILayout.Space();
    EditorGUILayout.EndHorizontal();

    // Draw the vertical bars.
    for (int i = 0; i < rt60s.arraySize; ++i) {
      float rt60 = rt60s.GetArrayElementAtIndex(i).floatValue;
      float barHeight = rt60 / maxRt60 * maxBarHeight;
      var rt60ValueTextRect = rt60ValueTextRects[i];

      // Align the left end and width with the text fields showing the rt60 values.
      var barRect = new Rect(rt60ValueTextRect.x, barAreaRect.y + maxBarHeight - barHeight,
                             rt60ValueTextRect.width, barHeight);
      EditorGUI.DrawRect(barRect, barColor);
    }
  }

  // Show the reverb modifiers.
  private void DrawReverbModifiers() {
    EditorGUILayout.LabelField(reverbPropertiesLabel);
    ++EditorGUI.indentLevel;
    EditorGUILayout.Slider(reverbGainDb, ResonanceAudio.minGainDb, ResonanceAudio.maxGainDb,
                           reverbGainLabel);
    EditorGUILayout.Slider(reverbBrightness, ResonanceAudio.minReverbBrightness,
                           ResonanceAudio.maxReverbBrightness, reverbBrightnessLabel);
    EditorGUILayout.Slider(reverbTime, 0.0f, ResonanceAudio.maxReverbTime, reverbTimeLabel);
    --EditorGUI.indentLevel;
  }
}
