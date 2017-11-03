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
using System.Collections;
using System.Collections.Generic;

/// A class that manages the room effects to be applied to spatial audio sources in the scene.
public static class ResonanceAudioRoomManager {
  /// Material type that determines the acoustic properties of a room surface.
  public enum SurfaceMaterial {
    Transparent = 0,              ///< Transparent
    AcousticCeilingTiles = 1,     ///< Acoustic ceiling tiles
    BrickBare = 2,                ///< Brick, bare
    BrickPainted = 3,             ///< Brick, painted
    ConcreteBlockCoarse = 4,      ///< Concrete block, coarse
    ConcreteBlockPainted = 5,     ///< Concrete block, painted
    CurtainHeavy = 6,             ///< Curtain, heavy
    FiberglassInsulation = 7,     ///< Fiberglass insulation
    GlassThin = 8,                ///< Glass, thin
    GlassThick = 9,               ///< Glass, thick
    Grass = 10,                   ///< Grass
    LinoleumOnConcrete = 11,      ///< Linoleum on concrete
    Marble = 12,                  ///< Marble
    Metal = 13,                   ///< Galvanized sheet metal
    ParquetOnConcrete = 14,       ///< Parquet on concrete
    PlasterRough = 15,            ///< Plaster, rough
    PlasterSmooth = 16,           ///< Plaster, smooth
    PlywoodPanel = 17,            ///< Plywood panel
    PolishedConcreteOrTile = 18,  ///< Polished concrete or tile
    Sheetrock = 19,               ///< Sheetrock
    WaterOrIceSurface = 20,       ///< Water or ice surface
    WoodCeiling = 21,             ///< Wood ceiling
    WoodPanel = 22                ///< Wood panel
  }

  /// A serializable dictionary class that maps surface materials from GUIDs. The dictionary is
  /// serialized to two lists, one for the keys (GUIDs) and one for the values (surface materials).
  [Serializable]
  public class SurfaceMaterialDictionary
      : Dictionary<string, SurfaceMaterial>, ISerializationCallbackReceiver {
    public SurfaceMaterialDictionary() {
      guids = new List<string>();
      surfaceMaterials = new List<SurfaceMaterial>();
    }

    /// Serializes the dictionary to two lists.
    public void OnBeforeSerialize() {
      guids.Clear();
      surfaceMaterials.Clear();
      foreach (var keyValuePair in this) {
        guids.Add(keyValuePair.Key);
        surfaceMaterials.Add(keyValuePair.Value);
      }
    }

    /// Deserializes the two lists and fills the dictionary.
    public void OnAfterDeserialize() {
      this.Clear();
      for (int i = 0; i < guids.Count; ++i) {
        this.Add(guids[i], surfaceMaterials[i]);
      }
    }

    // List of keys.
    [SerializeField]
    private List<string> guids;

    // List of values.
    [SerializeField]
    private List<SurfaceMaterial> surfaceMaterials;
  }

  /// Adds or removes a Resonance Audio room depending on whether the listener is inside |room|.
  public static void UpdateRoom(ResonanceAudioRoom room) {
    UpdateRoomEffectsRegion(new RoomEffectsRegion(room), IsListenerInsideRoom(room));
  }

  /// Removes a Resonance Audio room.
  public static void RemoveRoom(ResonanceAudioRoom room) {
    UpdateRoomEffectsRegion(new RoomEffectsRegion(room), false);
  }

  /// Adds or removes a Resonance Audio reverb probe depending on whether the listener is inside
  /// |reverbProbe|.
  public static void UpdateReverbProbe(ResonanceAudioReverbProbe reverbProbe) {
    UpdateRoomEffectsRegion(new RoomEffectsRegion(reverbProbe),
                            IsListenerInsideVisibleReverbProbe(reverbProbe));
  }

  /// Removes a Resonance Audio reverb probe.
  public static void RemoveReverbProbe(ResonanceAudioReverbProbe reverbProbe) {
    UpdateRoomEffectsRegion(new RoomEffectsRegion(reverbProbe), false);
  }

  // A class to encapsulate either a ResonanceAudioRoom or a ResonanceAudioReverbProbe. Only one of
  // |room| and |reverbProbe| is not null.
  private class RoomEffectsRegion : IEquatable<RoomEffectsRegion> {
    /// Currently active room/reverb probe.
    public ResonanceAudioRoom room = null;
    public ResonanceAudioReverbProbe reverbProbe = null;

    /// Constructs an instance that holds a Resonance Audio room.
    public RoomEffectsRegion(ResonanceAudioRoom room) {
      this.room = room;
      this.reverbProbe = null;
    }

    /// Constructs an instance that holds a Resonance Audio reverb probe.
    public RoomEffectsRegion(ResonanceAudioReverbProbe reverbProbe) {
      this.room = null;
      this.reverbProbe = reverbProbe;
    }

    /// Determines the equality of this room effects region and |other|. Used in List.Contains(),
    /// which would by default test for reference equality, while we only care if the encapsulated
    /// |room| and |reverbProbe| are the same.
    public bool Equals(RoomEffectsRegion other) {
      return this.room == other.room && this.reverbProbe == other.reverbProbe;
    }
  }

  // Container to store the candidate room effects regions in the scene.
  private static List<RoomEffectsRegion> roomEffectsRegions = new List<RoomEffectsRegion>();

  // Boundaries instance to be used in room detection logic.
  private static Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);

  // Adds or removes a room effects region and notifies the Resonance Audio class about the one to
  // be applied (or disables the room effects if there is none).
  private static void UpdateRoomEffectsRegion(RoomEffectsRegion roomEffectsRegion, bool isEnabled) {
    if (isEnabled && !roomEffectsRegions.Contains(roomEffectsRegion)) {
      roomEffectsRegions.Add(roomEffectsRegion);
    } else if (!isEnabled && roomEffectsRegions.Contains(roomEffectsRegion)) {
      roomEffectsRegions.Remove(roomEffectsRegion);
    }

    if (roomEffectsRegions.Count == 0) {
      ResonanceAudio.DisableRoomEffects();
      return;
    }

    var lastRoomEffectsRegion = roomEffectsRegions[roomEffectsRegions.Count - 1];
    if (lastRoomEffectsRegion.room != null) {
      ResonanceAudio.UpdateAudioRoom(lastRoomEffectsRegion.room);
    } else {
      ResonanceAudio.UpdateReverbProbe(lastRoomEffectsRegion.reverbProbe);
    }
  }

  /// Returns whether the listener is currently inside the given |room| boundaries.
  private static bool IsListenerInsideRoom(ResonanceAudioRoom room) {
    bool isInside = false;
    Transform listenerTransform = ResonanceAudio.ListenerTransform;
    if (listenerTransform != null) {
      Vector3 relativePosition = listenerTransform.position - room.transform.position;
      Quaternion rotationInverse = Quaternion.Inverse(room.transform.rotation);

      bounds.size = Vector3.Scale(room.transform.lossyScale, room.size);
      isInside = bounds.Contains(rotationInverse * relativePosition);
    }
    return isInside;
  }

  /// Returns whether the listener is currently inside the application region of the given
  /// |reverb_probe|, subject to the visibility test if |reverbProbe.onlyWhenVisible| is true.
  private static bool IsListenerInsideVisibleReverbProbe(ResonanceAudioReverbProbe reverbProbe) {
    Transform listenerTransform = ResonanceAudio.ListenerTransform;
    if (listenerTransform != null) {
      Vector3 relativePosition = listenerTransform.position - reverbProbe.transform.position;

      // First the containing test.
      if (reverbProbe.runtimeApplicationRegionShape ==
          ResonanceAudioReverbProbe.ApplicationRegionShape.Sphere) {
        if (relativePosition.magnitude > reverbProbe.GetScaledSphericalApplicationRegionRadius()) {
          return false;
        }
      } else {
        Quaternion rotationInverse = Quaternion.Inverse(reverbProbe.transform.rotation);
        bounds.size = reverbProbe.GetScaledBoxApplicationRegionSize();
        if (!bounds.Contains(rotationInverse * relativePosition)) {
          return false;
        }
      }

      // Then the visibility test.
      if (reverbProbe.onlyApplyWhenVisible) {
        if (ResonanceAudio.ComputeOcclusion(reverbProbe.transform) > 0.0f) {
          return false;
        }
      }
      return true;
    }
    return false;
  }
}
