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
using System.Collections;

/// Resonance Audio room component that simulates environmental effects of a room with respect to
/// the properties of the attached game object.
[AddComponentMenu("ResonanceAudio/ResonanceAudioRoom")]
public class ResonanceAudioRoom : MonoBehaviour {
  /// Room surface material in negative x direction.
  public ResonanceAudioRoomManager.SurfaceMaterial leftWall =
      ResonanceAudioRoomManager.SurfaceMaterial.ConcreteBlockCoarse;

  /// Room surface material in positive x direction.
  public ResonanceAudioRoomManager.SurfaceMaterial rightWall =
      ResonanceAudioRoomManager.SurfaceMaterial.ConcreteBlockCoarse;

  /// Room surface material in negative y direction.
  public ResonanceAudioRoomManager.SurfaceMaterial floor =
      ResonanceAudioRoomManager.SurfaceMaterial.ParquetOnConcrete;

  /// Room surface material in positive y direction.
  public ResonanceAudioRoomManager.SurfaceMaterial ceiling =
      ResonanceAudioRoomManager.SurfaceMaterial.PlasterRough;

  /// Room surface material in negative z direction.
  public ResonanceAudioRoomManager.SurfaceMaterial backWall =
      ResonanceAudioRoomManager.SurfaceMaterial.ConcreteBlockCoarse;

  /// Room surface material in positive z direction.
  public ResonanceAudioRoomManager.SurfaceMaterial frontWall =
      ResonanceAudioRoomManager.SurfaceMaterial.ConcreteBlockCoarse;

  /// Reflectivity scalar for each surface of the room.
  public float reflectivity = 1.0f;

  /// Reverb gain modifier in decibels.
  public float reverbGainDb = 0.0f;

  /// Reverb brightness modifier.
  public float reverbBrightness = 0.0f;

  /// Reverb time modifier.
  public float reverbTime = 1.0f;

  /// Size of the room (normalized with respect to scale of the game object).
  public Vector3 size = Vector3.one;

  void OnEnable() {
    ResonanceAudioRoomManager.UpdateRoom(this);
  }

  void OnDisable() {
    ResonanceAudioRoomManager.RemoveRoom(this);
  }

  void Update() {
    ResonanceAudioRoomManager.UpdateRoom(this);
  }

  void OnDrawGizmosSelected() {
    // Draw shoebox model wireframe of the room.
    Gizmos.color = Color.yellow;
    Gizmos.matrix = transform.localToWorldMatrix;
    Gizmos.DrawWireCube(Vector3.zero, size);
  }
}
