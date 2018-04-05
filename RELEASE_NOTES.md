# Release notes

## Resonance Audio SDK for Unity v1.2.1

### Bug fixes
* Fixed [issue #23](https://github.com/resonance-audio/resonance-audio-unity-sdk/issues/23) where a fatal error would be raised if a game object doesn't have an assigned mesh.
* Fixed [issue #24](https://github.com/resonance-audio/resonance-audio-unity-sdk/issues/24) where `ResonanceAudioBuildProcessor` gave a compiler error in Unity 2018.1.

### Other changes

* Major refactor and cleanup in room effects scripts.

## Resonance Audio SDK for Unity v1.2.0 (2018-02-20)

### Additions
* Added advanced near-field effect for sound sources less than 1 meter from the listener. Introduced `ResonanceAudioSource.nearFieldEffectEnabled` and `ResonanceAudioSource.nearFieldEffectGain` parameters to simulate the effect of sound sources being very close to the listener's ears. Note that this effect could result in up to ~9x gain boost on the source input. Therefore, it is advised to set smaller gain values or reduce the input gain for louder sound sources to avoid clipping of the output signal.
* Added `ResonanceAudioSource.occlusionIntensity` parameter to adjust the intensity of the occlusion effect.

### Behavioral Changes
* Significant CPU performance improvement for reverb times more than 0.6 seconds (thanks to a new spectral reverb implementation under the hood). Also, delivers a slightly brighter sounding reverb.
* Increased `ResonanceAudio.maxReverbTime` to 10 seconds for the `ResonanceAudioRoom.reverbTime` and `ResonanceAudioReverbProbe.reverbTime` modifiers.
* Material maps are stored as assets and are modified in the Inspector Window instead of the `Reverb Baking` window. Multiple maps per project are allowed, and the Reverb Baking Window selects one to be used in reverb computation. If you are updating the SDK from an older version, please make sure to remove the `ResonanceAudio` folder from the project assets before importing the new SDK to avoid potential asset conflicts.
* Improved the reverb gain adjustment parameter in `ResonanceAudioRoom` and `ResonanceAudioReverbProbe` to avoid noticeable delays with larger gain changes.

### Bug fixes
* Addressed [issue #16](https://github.com/resonance-audio/resonance-audio-unity-sdk/issues/16) where the `Reverb Baking` window would constantly lock up the editor in a project with a lot of materials. Note that there may still be a significant performance overhead if the `Visualize Mode` is enabled while making changes in the scene hierarchy, such as transform changes of a game object.
* Fixed [issue #11](https://github.com/resonance-audio/resonance-audio-unity-sdk/issues/11) where updating SDK would override material mappings.
* Fixed [issue #13](https://github.com/resonance-audio/resonance-audio-unity-sdk/issues/13) where the list of 'ResonanceAudioReverbProbe' was not scrollable in Reverb Baking Window.
* Fixed a bug where the ResonanceAudioAcousticMesh class would give an error when the scene contains non-triangular meshes (e.g. points and lines). Now non-triangular meshes will be skipped.

## Resonance Audio SDK for Unity v1.1.1 (2017-12-18)

### Bug fixes
* Fixed a bug in stereo deinterleaving input buffer conversion that could lead to a crash.

## Resonance Audio SDK for Unity v1.1.0 (2017-12-15)

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
* Fixed a depracation warning in `ResonanceAudioReverbBakingWindow` in Unity 2017.2+.

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
