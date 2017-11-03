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
using UnityEngine.Audio;
using System.Collections;

/// Resonance Audio source component that enhances AudioSource to provide advanced spatial audio
/// features.
[AddComponentMenu("ResonanceAudio/ResonanceAudioSource")]
[RequireComponent(typeof(AudioSource))]
[ExecuteInEditMode]
public class ResonanceAudioSource : MonoBehaviour {
  /// Audio rendering quality.
  public enum Quality {
    Stereo = 0,  ///< Stereo-only rendering
    Low = 1,  ///< Low quality binaural rendering (first-order HRTF)
    High = 2  ///< High quality binaural rendering (third-order HRTF)
  }

  /// Denotes whether the room effects should be bypassed.
  public bool bypassRoomEffects = false;

  /// Directivity pattern shaping factor.
  [Range(0.0f, 1.0f)]
  public float directivityAlpha = 0.0f;

  /// Directivity pattern order.
  [Range(1.0f, 10.0f)]
  public float directivitySharpness = 1.0f;

  /// Listener directivity pattern shaping factor.
  [Range(0.0f, 1.0f)]
  public float listenerDirectivityAlpha = 0.0f;

  /// Listener directivity pattern order.
  [Range(1.0f, 10.0f)]
  public float listenerDirectivitySharpness = 1.0f;

  /// Input gain in decibels.
  public float gainDb = 0.0f;

  /// Occlusion effect toggle.
  public bool occlusionEnabled = false;

  // Rendering quality of the audio source.
  public Quality quality = Quality.High;

  // Unity audio source attached to the game object.
  public AudioSource audioSource { get; private set; }

  // Native audio spatializer effect data.
  private enum EffectData {
    Id = 0,  /// ID.
    DistanceAttenuation = 1,  /// Computed distance attenuation.
    BypassRoomEffects = 2,  /// Should bypass room effects?
    Gain = 3,  /// Gain.
    DirectivityAlpha = 4,  /// Source directivity alpha.
    DirectivitySharpness = 5,  /// Source directivity sharpness.
    ListenerDirectivityAlpha = 6,  /// Listener directivity alpha.
    ListenerDirectivitySharpness = 7,  /// Listener directivity sharpness.
    Occlusion = 8,  /// Occlusion intensity.
    Quality = 9,  /// Source audio rendering quality.
    MinDistance = 10,  /// Minimum distance for distance-based attenuation.
    Volume = 11,  /// Volume.
  }

  // Current occlusion value;
  private float currentOcclusion = 0.0f;

  // Next occlusion update time in seconds.
  private float nextOcclusionUpdate = 0.0f;

  void Awake() {
    audioSource = GetComponent<AudioSource>();
  }

  void OnEnable() {
    // Validate the source output mixer route.
    AudioMixer mixer = (Resources.Load("ResonanceAudioMixer") as AudioMixer);
    if (mixer == null ||
        audioSource.outputAudioMixerGroup != mixer.FindMatchingGroups("Master")[0]) {
      Debug.LogWarning("Make sure AudioSource is routed to a mixer that ResonanceAudioRenderer " +
                       "is attached to.");
    }
  }

  void Update() {
    if (!occlusionEnabled) {
      currentOcclusion = 0.0f;
    } else if (Time.time >= nextOcclusionUpdate) {
      nextOcclusionUpdate = Time.time + ResonanceAudio.occlusionDetectionInterval;
      currentOcclusion = ResonanceAudio.ComputeOcclusion(transform);
    }
    UpdateSource();
  }

  // Updates the source parameters.
  private void UpdateSource() {
    if (audioSource.clip != null && audioSource.clip.ambisonic) {
      // Use ambisonic decoder.
      audioSource.SetAmbisonicDecoderFloat((int) EffectData.BypassRoomEffects,
                                           bypassRoomEffects ? 1.0f : 0.0f);
      audioSource.SetAmbisonicDecoderFloat((int) EffectData.Gain,
                                           ResonanceAudio.ConvertAmplitudeFromDb(gainDb));
      audioSource.SetAmbisonicDecoderFloat((int) EffectData.Volume,
                                           audioSource.mute ? 0.0f : audioSource.volume);
    } else if (audioSource.spatialize) {
      // Use spatializer.
      audioSource.SetSpatializerFloat((int) EffectData.BypassRoomEffects,
                                      bypassRoomEffects ? 1.0f : 0.0f);
      audioSource.SetSpatializerFloat((int) EffectData.Gain,
                                      ResonanceAudio.ConvertAmplitudeFromDb(gainDb));
      audioSource.SetSpatializerFloat((int) EffectData.DirectivityAlpha, directivityAlpha);
      audioSource.SetSpatializerFloat((int) EffectData.DirectivitySharpness, directivitySharpness);
      audioSource.SetSpatializerFloat((int) EffectData.ListenerDirectivityAlpha,
                                      listenerDirectivityAlpha);
      audioSource.SetSpatializerFloat((int) EffectData.ListenerDirectivitySharpness,
                                      listenerDirectivitySharpness);
      audioSource.SetSpatializerFloat((int) EffectData.Occlusion, currentOcclusion);
      audioSource.SetSpatializerFloat((int) EffectData.Quality, (float) quality);
      audioSource.SetSpatializerFloat((int) EffectData.MinDistance, audioSource.minDistance);
    }
  }

  void OnDrawGizmosSelected() {
    // Draw listener directivity gizmo.
    if (ResonanceAudio.ListenerTransform != null) {
      Gizmos.color = ResonanceAudio.listenerDirectivityColor;
      DrawDirectivityGizmo(ResonanceAudio.ListenerTransform, listenerDirectivityAlpha,
                           listenerDirectivitySharpness, 180);
    }
    // Draw source directivity gizmo.
    Gizmos.color = ResonanceAudio.sourceDirectivityColor;
    DrawDirectivityGizmo(transform, directivityAlpha, directivitySharpness, 180);
  }

  // Draws a 3D gizmo in the Scene View that shows the selected directivity pattern.
  private void DrawDirectivityGizmo(Transform target, float alpha, float sharpness,
                                    int resolution) {
    Vector2[] points = ResonanceAudio.Generate2dPolarPattern(alpha, sharpness, resolution);
    // Compute |vertices| from the polar pattern |points|.
    int numVertices = resolution + 1;
    Vector3[] vertices = new Vector3[numVertices];
    vertices[0] = Vector3.zero;
    for (int i = 0; i < points.Length; ++i) {
      vertices[i + 1] = new Vector3(points[i].x, 0.0f, points[i].y);
    }
    // Generate |triangles| from |vertices|. Two triangles per each sweep to avoid backface culling.
    int[] triangles = new int[6 * numVertices];
    for (int i = 0; i < numVertices - 1; ++i) {
      int index = 6 * i;
      if (i < numVertices - 2) {
        triangles[index] = 0;
        triangles[index + 1] = i + 1;
        triangles[index + 2] = i + 2;
      } else {
        // Last vertex is connected back to the first for the last triangle.
        triangles[index] = 0;
        triangles[index + 1] = numVertices - 1;
        triangles[index + 2] = 1;
      }
      // The second triangle facing the opposite direction.
      triangles[index + 3] = triangles[index];
      triangles[index + 4] = triangles[index + 2];
      triangles[index + 5] = triangles[index + 1];
    }
    // Construct a new mesh for the gizmo.
    Mesh directivityGizmoMesh = new Mesh();
    directivityGizmoMesh.hideFlags = HideFlags.DontSaveInEditor;
    directivityGizmoMesh.vertices = vertices;
    directivityGizmoMesh.triangles = triangles;
    directivityGizmoMesh.RecalculateNormals();
    // Draw the mesh.
    Vector3 scale = 2.0f * Mathf.Max(target.lossyScale.x, target.lossyScale.z) * Vector3.one;
    Gizmos.DrawMesh(directivityGizmoMesh, target.position, target.rotation, scale);
  }
}
