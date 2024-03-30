# LonelyBoid
*A 2D boids library on compute shaders, multi-flock and force fields*

This is largely inspired by Sebastian Lague's [Boids](https://github.com/SebLague/Boids), check out the [Coding Adventure](https://www.youtube.com/watch?v=bqtqltqcQhw).

**Disclaimer:** this project was created as a one-shot experiment. I do not plan on doing regular updates nor fixes, but if you want to contribute, I'll gladly merge useful changes. I do not regularly use Unity, nor regularly code in C#; I did try to keep code neat though. Check the [known limitations](#known-bugs-and-limitations) section.

## Samples

### Repel
This sample scene has a single flock and a radial force with intensity set to zero. On click, the center of the radial force is set to the cursor's position, and the intensity raised. At the same time, an animation is kicked off, boids cohesion is lowered, and max speed raised. This results in the boids quickly escaping from the clicked point.

https://github.com/5p4k/LonelyBoid/assets/3787759/30f1176b-aa76-4a40-a24f-a102806da99f

### Predators
This sample scene has two flocks with physics enabled. Cross-flock interaction is set in a way that the red boids have a drive to align and be cohese with the green boids. The green boids, on the other hand, are pushed away from the red. There is a slight difference in speed. The result is that the red boids chase the green boids. Colliders are set up so that when a red boid hits a green, the green is killed. In particular, the red boids are kinematic rigid bodies and are animated through Unity's physics.

https://github.com/5p4k/LonelyBoid/assets/3787759/8cb47c59-6fe4-4efb-87fa-8c30162ccb0f

## What can this do
* **Classical boids drives**  
  Control cohesion, alignment and avoidance of every flock.
* **Cross-flock drives**  
  A custom cohesion, alignment and avoidance value can be set from a flock towards members of other flocks.
* **Life cycle**  
  Spawn boids regularly within a circular domain until reaching capacity, and kill them if they exit the domain.
* **Survival drive**  
  Boids can try to avoid the circular domain's border.
* **Custom forces**  
  You can apply custom forces (currenly only a radial force and a turbulent force based on simplex noise) to spice things up. Each flock has its own coefficient for each force.
* **Speed and acceleration constraints**  
  Max/min speed, max acceleration, max angular acceleration to reduce boids jerking left and right.
* **Animatable**  
  You can animate the drive coefficients and thus customize the behavior of the flock, e.g. escaping quickly or getting more cohese.
* **Visibility and avoidance settings**  
  The field of view and the radius at which the boid can "see" for cohesion and alignment (visibility) or avoidance can be customized.
* **Physics compatibility**  
  Boids can be kinematic (not dynamic!) rigid bodies, so it plays nicely with Unity physics engine and collision detection.
* **Performance**  
  At every frame, the calculations for the boids are performed on a compute shader in parallel.
* **Orbit visualization**  
  You can live preview the orbits the boids will take to get a sense of whether your setup is working.

## Known bugs and limitations
Many of these things *could* be fixed or implemented, so if you are interested to tackle that, scroll all the way to the [how do I](how-do-I) section.

What this cannot do:
* **3D boids**  
  This was born for 2D, there is really no support for 3D.
* **Obstacle avoidance**  
  Obstacle avoidance is not implemented. The boids will go straight through.
* **Dynamic rigid bodies**  
  Dynamic rigid bodies are not supported. At the moment you can set to use kinematics, but it will teleport the boid into the new position, which causes Unity to recalculate all interactions (and potentially skip some). You *can* use them, but it will not work well, and if you do, set mass and drag to zero.

Other limitations:
* **Pressing play can cause some memory to be leaked**  
  It's the memory used to store the orbits. This will not happen in play mode.
* **Inspector interface is somewhat cumbersome**  
  I had no patience to make it super nice and shiny, I really hope it is good enough.
* **Documentation**  
  There is not much documentation, hopefully this is enough. Do try the samples out. If you need code documentation/explanation, feel free to open an issue.
* **Only radial and turbulent forces**  
  That's the only ones I implemented so far.
* **Limited extensibility**
  Since the whole project is small and open source, the main way to extend it would be to fork or make a PR. However I did try to make a few methods virtual, so perhaps you can just override them.
* **Orbits settings do not save**  
  They do not, because the orbits storage is static, and I cannot figure out how Unity constructs and uses its Editors.
* **Pre-spawn some boids in the editor**  
  Not impossible, but since most of the simulation is sort-of random, I did not consider that important.
* **Change the way/position/velocity boids spawn**  
  You can spawn them by hand and then alter their position, but by default they spawn with random position, speed, direction.
* **Unit tests**  
  There are no unit tests. You can always contribute one :)
* **Using a space-partitioning data structure**
  Compute shader was good enough, but for a very large amount of boids this will probably be needed.

### How do I...
You want to do some of the things above? That's great, here's some hint at getting started.

**3D boids** TODO

**Obstacle avoidance** TODO

**Dynamic rigid bodies** TODO

**Pressing play can cause some memory to be leaked.** I really do not understand Unity's even system. Some of them are virtual methods, some of them are `On...` methods, some of them get called in the editor, some not. The issue here is that you would need a `OnDestroy` method that is either static, and can destroy the statically-allocated resources for storing the orbits, or one that is guaranteed to be called when you are *really, really* done and are about to swith to play mode, exit play mode and so on. In that spot, you should call the `Release` method of all the `DualBuffer` in the `Force/FlockOrbitsManager`. Alternatively, if you understand Unity's Editor lifecycle, you can restructure the editors such that the buffers are allocated when the editor starts, and deallocated when the editor closes. That could also be on a `FlockEditor` class or `ForceEditor` class. Alternatively, one could be more wasteful and just allocated them every time the editor is enabled, and deallocate them when it's disabled. That would work too.

**Inspector interface is somewhat cumbersome** PRs are welcome! I have no suggestion on how to go about that because I don't know Unity well myself.

**Documentation** PRs are welcome!

**Only radial and turbulent forces** You cannot exactly "write your own force" unless you want to manually do the physics calculation. The easiest way here is to extend the package by creating a new force. To do so, define a new `ForceType`, add the attributes you need, and modify `ForceEditor` so that it displays it nicely. You need to add the same fields to `IO.ForceData` so that it can be transferred onto the compute shader. Then edit `LonelyBoid.cginc`. You need to add the same fields, in the same order, to `Lonely::IO::ForceData` and implement the HLSL code for calculating the force on a boid (see `Lonely::RadialForce::compute`). If your force depends on the boid's orientation, please modify *all* compute methods. Then double-check the other shaders to make sure they're rendered consistently.

**Limited extensibility** PRs are welcome! I'm sure that from a package user perspective it's easier to see what changes are needed.

**Orbits settings do not save** I found no place in Unity where you could store "static, project-level settings". You can use an asset, but I cannot put write to it in the package, to my understanding. You could add Editor-only fields to every flock and force, but that does pollute the runtime code a bit. I found the orbits useful to debug, and it did not seem a big deal having to disable them every time, but if you find that annoying and have a good solution (e.g. a separate editor-only component), feel free to open a PR.

**Pre-spawn some boids in the editor** The only issue here is that the boid needs to be attached to the flock two-ways: they need to be in the `_activeBoids` (or at least in the `_spawnQueue`) as well as having their (hidden) property `flock` set to the flock. The best would probably be to expose the flock property on the boid, or have a "spawn" button in the inspector. On `Start`, all `Boid` components with the given `flock` as owner can be collected and added to `_activeBoids`. If you need to support killing or changing the owning flock, then after spawning, in `Update`, you should scan through the active boids and remove those that have a different flock or that have become `null`. Even better would be killing them through the `Kill` method and specify an extra argument on whether they should return to the pool or not (this can be useful if you have different models for the boids). See also the item below:

**Change the way/position/velocity boids spawn** The best way here would be to add a `OnSpawn` virtual method, default it to set random position, orientation and speed, and allow to override it in subclasses. Alternatively, spawn it yourself and add it to the simulation with an extra `Add` method (which should add it to the spawn queue, to avoid messing up the indices when completing the update shader). Note that if you want to have extra freedom to add and remove the boids, it might be a good idea to remove the spawn and kill queues, and instead have `_activeBoids` being copied every time a new compute shader is kicked off (and use the copy when applying the updates), so that any other component is free to modify the list of active boids, and they will be picked up at the next frame by the simulation.

**Unit tests** PRs are welcome!

**Using a space-partitioning data structure** I think that the compute shader is valuable to have, so if you want e.g. a quad tree, it would be very good to implement it in a way that it can be bulk-transferred to the compute shader and queried there. In the simplest form, the nodes on the tree can be indexed into a `DualBuffer`. The boids would probably then be sorted using a DFS search into the tree so that they can be packed into another `DualBuffer` and the nodes can hold indices to the range of boids they contain. Of course if it's fast enough it could still be possible to drop the compute shader and do everything in C#, but I think that having a massively parallel querying, plus the fact that compute shaders can be started asynchronously, is going to be way faster.
