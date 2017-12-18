# Release notes

## Resonance Audio SDK for Unity v1.1.1

### Bug fixes
* Fixed a bug in stereo deinterleaving input buffer conversion that could lead to a crash.

## Resonance Audio SDK for Unity v1.1.0

### Additions
* Added multi-object editing support to `ResonanceAudioReverbProbe`.

### Behavioral Changes
* The reverb brightness and time modifiers in `ResonanceAudioRoom` adjust the room effects more accurately now for long reverb tails.
* Improved reverb baking precision.

### Bug fixes
* Fixed [issue #7](https://github.com/resonance-audio/resonance-audio-unity-sdk/issues/7) where the `GvrAudio*` components would cause a crash in the Unity Editor when the target platform is selected to iOS.
* Fixed [issue #2](https://github.com/resonance-audio/resonance-audio-unity-sdk/issues/2) where `ResonanceAudioRoomManager` would cause GC Allocs in each update call.
* Fixed [issue #1](https://github.com/resonance-audio/resonance-audio-unity-sdk/issues/1) where the directivity gizmo of the `ResonanceAudioSource` component would introduce a memory leak at run time.
* Fixed [issue #4](https://github.com/resonance-audio/resonance-audio-unity-sdk/issues/4) where the enable callback of each `ResonanceAudioSource` component would cause GC Allocs at run time.
* Fixed [issue #8](https://github.com/resonance-audio/resonance-audio-unity-sdk/issues/8) where the Unity Editor could occasionally crash when the `ResonanceAudioReverbProbe` component is used in the scene.
* Fixed an issue where the `ResonanceAudioListener` would unnecessarily make update calls in Edit Mode.

### Other Changes
* For projects upgrading from Google VR Audio to Resonance Audio, the SDK will now detect incompatible assets at build time and offer to remove them. See additional [upgrade instructions](https://developers.google.com/resonance-audio/migrate/).

## Resonance Audio SDK for Unity v1.0.0 (2017-11-06)

This is the initial release of Resonance Audio SDK for Unity, which includes:
* 3D audio spatialization
* Playback and recording of Ambisonic audio files
* Room effects rendering with custom surface materials, using either:
  * a real-time room model
  * precomputed scene geometry-based model
* Occlusion
* Sound directivity controls
* Source spread controls
* Distance attenuation

* Near-field rendering
