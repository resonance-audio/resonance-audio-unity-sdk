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

/// Resonance Audio reverb probe component that acts as a sample point where the reverb properties
/// are computed by casting rays that interact with surrounding geometries and collecting them
/// back. In addition, it allows the user to define a "region of application", where when the
/// listener enters, the pre-computed reverb properties are applied.
[AddComponentMenu("ResonanceAudio/ResonanceAudioReverbProbe")]
[ExecuteInEditMode]
public class ResonanceAudioReverbProbe : MonoBehaviour {
  /// Supported choices of the shape of regions of application.
  public enum ApplicationRegionShape {
    Sphere = 0,
    Box = 1
  }

  /// The RT-60s of the reverb baked in this probe.
  public float[] rt60s = new float[] {0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f};

  /// Reverb gain modifier in decibels.
  public float reverbGainDb = 0.0f;

  /// Reverb brightness modifier.
  public float reverbBrightness = 0.0f;

  /// Reverb time modifier.
  public float reverbTime = 1.0f;

  /// Which shape of regions of application to use at runtime to check whether the listener is
  /// inside.
  public ApplicationRegionShape runtimeApplicationRegionShape = ApplicationRegionShape.Box;

  /// Size of the box-shaped region of application, normalized with respect to scale of the game
  /// object.
  public Vector3 boxApplicationRegionSize = Vector3.one;

  /// Radius of the sphere-shaped region of application.
  public float sphereApplicationRegionRadius = 1.0f;

  /// Only apply the reverb properties if this probe is visible to the listener.
  public bool onlyApplyWhenVisible = true;

  /// Proxy room related fields. A proxy room is used to calculate real-time early reflections.
  /// The position of the proxy room in world space.
  public Vector3 proxyRoomPosition = Vector3.zero;

  /// The rotation of the proxy room in world space.
  public Quaternion proxyRoomRotation = Quaternion.identity;

  /// The size of the proxy room in world space.
  public Vector3 proxyRoomSize = Vector3.one;

  /// The surface materials on the six walls of the proxy room in the
  /// {-x, +x, -y, +y, -z, +z}-directions.
  public ResonanceAudioRoomManager.SurfaceMaterial proxyRoomLeftWall =
      ResonanceAudioRoomManager.SurfaceMaterial.Transparent;
  public ResonanceAudioRoomManager.SurfaceMaterial proxyRoomRightWall =
      ResonanceAudioRoomManager.SurfaceMaterial.Transparent;
  public ResonanceAudioRoomManager.SurfaceMaterial proxyRoomFloor =
      ResonanceAudioRoomManager.SurfaceMaterial.Transparent;
  public ResonanceAudioRoomManager.SurfaceMaterial proxyRoomCeiling =
      ResonanceAudioRoomManager.SurfaceMaterial.Transparent;
  public ResonanceAudioRoomManager.SurfaceMaterial proxyRoomBackWall =
      ResonanceAudioRoomManager.SurfaceMaterial.Transparent;
  public ResonanceAudioRoomManager.SurfaceMaterial proxyRoomFrontWall =
      ResonanceAudioRoomManager.SurfaceMaterial.Transparent;

  void OnEnable () {
    ResonanceAudioRoomManager.UpdateReverbProbe(this);
  }

  void OnDisable () {
    ResonanceAudioRoomManager.RemoveReverbProbe(this);
  }

  void Update () {
    ResonanceAudioRoomManager.UpdateReverbProbe(this);
  }

  /// Draw either a box or sphere wire-frame depending on the runtime shape of the region of
  /// application.
  void OnDrawGizmosSelected() {
    Gizmos.color = Color.magenta;
    switch (runtimeApplicationRegionShape) {
    case ApplicationRegionShape.Sphere:
      Gizmos.DrawWireSphere(transform.position, GetScaledSphericalApplicationRegionRadius());
      break;
    case ApplicationRegionShape.Box:
      Gizmos.matrix = transform.localToWorldMatrix;
      Gizmos.DrawWireCube(Vector3.zero, boxApplicationRegionSize);
      break;
    }
  }

  /// Set proxy room properties. Proxy rooms are estimated by the ray-tracing engine and passed
  /// back to be used in real-time early reflections.
  public void SetProxyRoomProperties(ResonanceAudio.RoomProperties proxyRoomProperties) {
    proxyRoomPosition = new Vector3(proxyRoomProperties.positionX,
                                    proxyRoomProperties.positionY,
                                    proxyRoomProperties.positionZ);
    proxyRoomRotation = new Quaternion(proxyRoomProperties.rotationX,
                                       proxyRoomProperties.rotationY,
                                       proxyRoomProperties.rotationZ,
                                       proxyRoomProperties.rotationW);
    proxyRoomSize = new Vector3(proxyRoomProperties.dimensionsX,
                                proxyRoomProperties.dimensionsY,
                                proxyRoomProperties.dimensionsZ);
    proxyRoomLeftWall = proxyRoomProperties.materialLeft;
    proxyRoomRightWall = proxyRoomProperties.materialRight;
    proxyRoomFloor = proxyRoomProperties.materialBottom;
    proxyRoomCeiling = proxyRoomProperties.materialTop;
    proxyRoomBackWall = proxyRoomProperties.materialBack;
    proxyRoomFrontWall = proxyRoomProperties.materialFront;
  }

  /// Gets the radius of the spherical region of application scaled by the transform. In order to
  /// maintain the spherical shape, the maximum of the scales in three dimensions is used to
  /// scale the radius (similar to how Unity handles Sphere Collider).
  public float GetScaledSphericalApplicationRegionRadius() {
    Vector3 scale = transform.lossyScale;
    float maxScale = Mathf.Max(scale.x, scale.y, scale.z);
    return sphereApplicationRegionRadius * maxScale;
  }

  /// Gets the size of the box-shaped region of application scaled by the transform.
  public Vector3 GetScaledBoxApplicationRegionSize() {
    return Vector3.Scale(transform.lossyScale, boxApplicationRegionSize);
  }
}
