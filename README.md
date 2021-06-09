# Jyazo C#
This application is a complete rewrite for the client side of
the following [project](https://github.com/sabihismail/Jyazo-Screenshot)
completed quickly in under a day.

I have migrated the client to C# because of the problems I was facing
with Java's limited functionality and the annoyance of dealing with
Swing on JavaFX. Knowing I would be targetting Windows for the most part, 
I've decided that this will be the client from here onwards.

The server and the old Java client will still be visible on the above
linked project. There are no comments on this project but functions
are very similar so comments from the previous project would still
apply.

Gfycat is no longer being used as the AnimatedGif library allow
for optimized and reliable GIF creation as opposed to the
inconsistency I was facing in Java and with the GIFCreator library.

Current Tasks:
1. <s>Move from generic hardcoded unique password to OAuth2 flow</s> - Done
2. <s>Add multi monitor support</s> - Done
3. <s>Use global mouse/keyboard hooks</s> - Done
4. Implement Fullscreen Game Screenshot taking (DirectX 9, 10, 11, 12)
   * Potentially add support for Vulkan/OpenGL

## Built With
* [AnimatedGif](https://github.com/mrousavy/AnimatedGif) - Used to create GIFs more efficiently than before
* [MouseKeyHook](https://github.com/gmamaladze/globalmousekeyhook) - Global Low Level Mouse/Keyboard Hooking utility
* [BetterFolderBrowser](https://github.com/Willy-Kimura/BetterFolderBrowser) - Folder Browser that isn't extremely limited like the WPF one
* [Newtonsoft.Json](https://github.com/JamesNK/Newtonsoft.Json) - JSON Parser
* [CefSharp](https://github.com/cefsharp/CefSharp) - Web Browser WPF Component used for OAuth2 Flow
* [SharpDX](https://github.com/sharpdx/SharpDX) - Used for DirectX drawing
* [EasyHook](https://github.com/EasyHook/EasyHook) - Used for DirectX Hooking
* [Direct3DHook](https://github.com/spazzarama/Direct3DHook) - Implementation code for DirectX Hooking that I will merge into this project

## Attributions
* Tray Icon Image (Name: Share Screen) by Chinnaking from the Noun 
Project. The image was resized, edited, and the colours were changed.
Link: https://thenounproject.com/search/?q=screen%20share&i=1050685
