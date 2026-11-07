# Third-party notices

## FluidR3 GM SoundFont

MelodyFlow expects a locally obtained `FluidR3_GM.sf2` for the default MIDI render path. The original Fluid R3 SoundFont is attributed to Frank Wen, copyright 2000–2002 (and 2008 in the preserved upstream notice), and is released under the MIT license. Keep the supplied copyright and permission notice with the SoundFont and any redistribution. A public license reference reproducing the original README/COPYING and attribution is available in the [MuseScore FluidR3 license notice](https://github.com/musescore/MuseScore/blob/main/share/sound/FluidR3Mono_License.md).

The `.sf2` binary is not included in this repository. When downloading it, use a source that provides the corresponding license notice and verify that the file is the original FluidR3 GM or a clearly licensed compatible version. The Worker stores this document reference in `MusicTrack.LicenseEvidenceKey` by default; it does not establish the provenance of a user-downloaded binary automatically.

## LM Studio and model files

LM Studio supports local inference without a per-call API charge and is free for home and work use according to its [official announcement](https://lmstudio.ai/blog/free-for-work). The downloaded language model has separate license terms and hardware requirements; review those before commercial use. MelodyFlow does not bundle LM Studio or any model.

For any monetized YouTube output, check the exact model version's current license and the terms of every other selected tool/resource for commercial use and intended distribution before choosing it and before publication. Keep a record of the source and terms, and preserve and supply all required attributions/notices. “License” means terms of use; it is not a software version or a key to enter. Prefer free options, but neither zero total cost nor commercial permission is automatic. Do not treat this file's general references as proof that a particular downloaded file is cleared.

## FluidSynth and FFmpeg

FluidSynth and FFmpeg are external executables installed by the user and are not bundled in this repository. FluidSynth's source is distributed under the LGPL; check the license and build options for the Windows binary selected in the [official repository](https://github.com/FluidSynth/fluidsynth).

FFmpeg's codebase is primarily LGPL 2.1-or-later, but optional GPL components can change the applicable terms for a particular build. Check the license information for the exact [FFmpeg build](https://ffmpeg.org/legal.html) installed. MelodyFlow currently invokes the local executables and does not redistribute them.

## YouTube disclosure and monetization

Before publishing, check YouTube's [current AI disclosure guidance](https://support.google.com/youtube/answer/14328491) and [channel monetization policies](https://support.google.com/youtube/answer/1311392). The disclosure guidance currently lists AI-generated music as content creators need to disclose. Monetization review also considers original/authentic content and reused or mass-produced content; disclosure itself does not guarantee eligibility.
