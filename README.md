# MoonlightVRC

## Idea

When creating a World for VRChat that reveals items around the player as they walk up to them, I stumbled across the problem that Quest doesn't handle realtime lights well. As a result, I may have ended up spending tens of hours coding my own light system.

What this includes:
- Point/spotlights editable at runtime.
- A couple of premade shaders (standard, particle).
- Premade code handling lights, normals and a Lambertian diffuse.

Work in progress:
- Water shader
- Documentation
- More performance testing/improvements
- Shadowcaster planes
  
Planned:
- Support for addative baked light maps and ambient lighting in the standart shader.
  
---

## Performance

Early testing showed the Quest 3 dropping to around 30 FPS when having 100 spotlights active in a scene at once. This test was conducted with 5 material targets. Since then the shader has grown in size significantly there has been optimisa

On PC, I haven't encountered any frame drops in the ediotherLightSources'tor at all, even with 400 concurrent lights.

## Quick start

1. Clone the code into your project.

2. Add the `PlayerPositionsToShader` component to a GameObject in your scene:
   - Tweak strength/intensity of the local and remote player if you want them to have an attached light.

3. For lights, attach `LightdataStorage` to a Transform and configure:
   - `range`, `type`, `color`, `intensity`, and `spotAngleDeg`.
  
4. Add the light transform to your `PlayerPositionsToShader` component's `otherLightSources` array.

5. Use one of the premade shaders on your material. Or if you feel like it, use the provided .hlsl/.cginc in your own shader. You just need to copy everything surrounded by Moonlight comments, and apply it at the end of your shader.

---

## Editor preview

- While not in Play mode, the editor helper `PlayerPositionsToShaderPreview` (EditorPreview/Editor/PlayerPositionsToShaderPreview.cs) and `ShadowcasterUpdaterPreview` (EditorPreview/Editor/ShadowcasterUpdaterPreview.cs) write the same property blocks to assigned Renderers so you can preview lighting effects in the Scene view. Those update 10 times a second.
- The editor partial helper for building preview arrays is in `EditorPreview/PlayerPositionsToShader.Editor.cs`.

---

## Tips

- Match `maxLights` in the component with the `#define MAX_LIGHTS` in your shaders (default is 80 — see Performance).

---

## Contributing

If you want to help with development, please contact me on Discord (@demuenu) so we can coordinate our efforts.
If your somebody with a education in Computer graphics, I would be even more thankfull for your help. As right now, it's just me with ~1.5 years of messing around in Shaderlab and ChatGPT as my advisor. So I'm sure that there are serious flaws in the codebase :-)