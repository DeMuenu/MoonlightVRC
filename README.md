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
  
Planned:
- Basic shadows via a shadow emitter map. The plan is to only sample the highest points of a map area and then calculate if the light ray is intersecting the object. This should provide basic shadow casting that is much more performant than raycasting.
- Support for addative baked light maps and ambient lighting.
  
---

## Performance

Early testing showed the Quest 3 dropping to around 30 FPS when having 100 spotlights active in a scene at once. This test was conducted with 5 material targets. Since then the shader has grown in size significantly and I have also included some optimizations in the code transferring light data to the objects. Reading the light data and transferring it to the shader seems to be a major bottleneck at the moment but there is room for improvements.

On PC, I haven't encountered any frame drops in the editor at all, even with 400 concurrent lights.

## Quick start

1. Clone the code into your project.

2. Add the `PlayerPositionsToShader` component to a GameObject in your scene:
   - Inspect the script in the inspector and assign `targets` (Objects that use a compatible shader) and optional `otherLightSources` (Transforms as described in step 3).
   - Tweak strength/intensity of the local and remote player if you want them to have an attached light.

3. For lights, attach `LightdataStorage` to a Transform and configure:
   - `range`, `type`, `color`, `intensity`, and `spotAngleDeg`.

4. Use one of the premade shaders on your objects. Or if you feel like it, use the provided .hlsl in your own shader. You just need to copy everything surrounded by Moonlight comments, and applying it at the end of your shader.

---

## Editor preview

- While not in Play mode, the editor helper `PlayerPositionsToShaderPreview` (EditorPreview/Editor/PlayerPositionsToShaderPreview.cs) writes the same property blocks to assigned Renderers so you can preview emissive/lighting effects in the Scene view. Those update 10 times a second.
- The editor partial helper for building preview arrays is in `EditorPreview/PlayerPositionsToShader.Editor.cs`.

---

## Tips

- Match `maxLights` in the component with the `#define MAX_LIGHTS` in your shaders (default is 80 — see Performance).

---

## Contributing

If you want to help with development, please contact me on Discord (@demuenu) so we can coordinate our efforts.
