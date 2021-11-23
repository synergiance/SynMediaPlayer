# SynMediaPlayer
An advanced Udon based media player for VRChat.

# Features
- Gapless playback
- Stream players
- Low Latency selection in game, for your RTSPT streams
- Ability to lock the video player
- World moderators support, including the ability to exclude instance master
- Automatic retrying of video URLs
- Automatic resynchronization, though a manual button is included to help if any deeper issues arise
- Retry loading video button
- Looping support
- Interactive seek bar
- Aspect ratio selection

# How to set up
1. Download the unity package from the releases page
2. Before dragging anything into your scene, add a TextureWorkaround component to a gameobject high in your scene hierarchy (above where you'll be placing the screen)
3. Add the 5 render textures to the TextureWorkaround component's texture array, and disable the gameobject.
4. Drag in the SMP Video Players prefab (any one) anywhere
5. Drag in the SMP UI (any one) anywhere else
6. Expand the SMP UI gameobject and the Logic gameobject to reveal the Video Player and Control Panel objects.
7. Drag the video player object to the SMP Video Players callback slot
8. Drag the SMP Video Players object to the video player's media players slot
9. You may disable debug logging and diagnostics or enable them

# How to use this git repo
1. Clone the repo to anyplace on your computer
2. Add the project to Unity Hub and open it in the current VRChat version of unity.
3. Install the VRChat SDK
4. Install UdonSharp
5. You should be all set up at this point and the test scene should work fine
6. (Optional) You may export the Assets/SynMediaPlayer folder to a unity package to use in your other projects.
