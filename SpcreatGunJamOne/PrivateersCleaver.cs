using Gungeon;
using ItemAPI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SpcreatGunJamOne
{
    class PrivateersCleaver : BetterGunBehaviour
    {
        // Textures for the particles, skull and bone models. These are loaded in when the cleaver is added.
        public static Texture2D particleTexture;
        public static Texture2D skullTexture;
        public static Texture2D boneTexture;

        public static void Init()
        {
            // Standard gun setup
            Gun gun = ETGMod.Databases.Items.NewGun("Privateer's Cleaver", "spccleaver");
            Game.Items.Rename("outdated_gun_mods:privateer's_cleaver", "spc:privateer_cleaver");
            gun.gameObject.AddComponent<PrivateersCleaver>();
            GunExt.SetShortDescription(gun, "Dead Men Tell No Tales");
            GunExt.SetLongDescription(gun, "\n\nAn old boarding axe, forged and abandoned by the followers of Cannonbalrog. Though the blade is pristine, it has a strong sulfuric odor.\n\nWhen swung, fires a low damage cannonball. Continuously swinging after your initial swing allows you to dash forward and combo the enemy you hit with the cannonball. Combo attacks will do more damage with each consecutive hit.");
            GunExt.SetupSprite(gun, null, "spccleaver_idle_001", 8);

            // Load some textures for later use
            particleTexture = ResourceExtractor.GetTextureFromResource("SpcreatGunJamOne/Resources/default-particle2.png");
            skullTexture = ResourceExtractor.GetTextureFromResource("SpcreatGunJamOne/Resources/skull2.png");
            boneTexture = ResourceExtractor.GetTextureFromResource("SpcreatGunJamOne/Resources/bone.png");

            // PROJECTILE SETUP SECTION -- for the most part just standard copy of klobbe projectile.
            var mod = GunExt.AddProjectileModuleFrom(gun, "klobb", true, false);
            mod.shootStyle = ProjectileModule.ShootStyle.SemiAutomatic;
            mod.angleVariance = 0f;
            mod.ammoCost = 0;
            mod.cooldownTime = 1000000.35f; // functionally infinite cooldown time. It gets manually reset when you stop your combo swing. I do this because otherwise you would start shooting projectiles in the middle of your combo.
            mod.numberOfShotsInClip = -1; // make it so you dont have to reload

            // Instantiate the projectile as a fakeprefab so I can make changes to it
            Projectile projectile = UnityEngine.Object.Instantiate<Projectile>(gun.DefaultModule.projectiles[0]);
            projectile.gameObject.SetActive(false);
            FakePrefab.MarkAsFakePrefab(projectile.gameObject);
            UnityEngine.Object.DontDestroyOnLoad(projectile);
            gun.DefaultModule.projectiles[0] = projectile;

            projectile.baseData.damage *= 0.5f;
            projectile.baseData.speed *= -0.75f; // I do this so that the projectile will intially go backwards when fired
            projectile.transform.parent = gun.barrelOffset;

            projectile.SetProjectileSpriteRight("spccleaver_projectile_001", 13, 13, null, null);
            // -- END OF PROJECTILE SETUP SECTION

            gun.reloadTime = 1.7f;
            GunExt.SetAnimationFPS(gun, 20); // fast animation because it's gotta be smooth, man

            // ANIMATION SETUP SECTION --
            // Code to make the shoot animation loop. Typically making a looping shoot animation would result in only being able to fire your gun once, but the next section of code is what handles that.
            var clip = gun.spriteAnimator.GetClipByName(gun.shootAnimation);
            clip.wrapMode = tk2dSpriteAnimationClip.WrapMode.LoopSection;
            clip.loopStart = 9;

            // Adds events to the shoot animation. This is what allows me to break out of the infinitely looping shoot animation by checking to see if you clicked or not.
            clip.frames[3].eventInfo = "removeClicked"; // This event makes it so that your initial click to fire the gun doesn't count towards your click counter, so that it won't give you a free melee swing after you shoot.
            clip.frames[3].triggerEvent = true;
            clip.frames[10].eventInfo = "clickCheck"; // This event checks to see whether you've clicked since the last check. The clicks are handled in Update, and the checks are handled in AnimationEventTriggered.
            clip.frames[10].triggerEvent = true;
            clip.frames[15].eventInfo = "clickCheck";
            clip.frames[15].triggerEvent = true;

            // Adds events to the intro animation, so that when the intro animation plays it resets the cooldown of your projectile, so that you can fire another cannonball.
            var introClip = gun.spriteAnimator.GetClipByName(gun.introAnimation);
            introClip.frames[0].eventInfo = "refreshCooldown";
            introClip.frames[0].triggerEvent = true;
            // -- END OF ANIMATION SETUP SECTION

            // More standard gun stuff
            gun.spriteAnimator.MuteAudio = false;
            gun.InfiniteAmmo = true;
            gun.quality = PickupObject.ItemQuality.B;
            gun.barrelOffset.transform.localPosition = new Vector3(2f, 1.1f, 0f);
            gun.encounterTrackable.EncounterGuid = "spccleaver";
            gun.gunClass = GunClass.NONE;
            gun.muzzleFlashEffects = null;
            ItemBuilder.AddPassiveStatModifier(gun, PlayerStats.StatType.Curse, 2f);
            ETGMod.Databases.Items.Add(gun, null, "ANY");
        }
        
        bool clicked = false; // This variable keeps track of whether or not you clicked since last click check.
        AIActor highlightedEnemy = null; // This variable holds the currently targeted enemy. An enemy gets targeted by getting hit by a projectile.

        // This is your assault level. It's used in the click checks, to see how much your combo is.
        // -1 means the enemy has not been dashed at or slashed at.
        // Every other value represents your combo multiplier, which affects how much damage you do every swing.
        int assaultLevel = -1; 

        // This is where my BetterGunBehaviour comes in, allowing me to override Start.
        public override void Start()
        {
            base.Start();
            gun.spriteAnimator.AnimationEventTriggered += this.AnimationEventTriggered; // Attach to the AnimationEventTriggered event to handle animation events
        }

        // Heres where a lot of the real function of the gun lies, in the animation events.
        // This is where the gun's sound plays, where the dash happens, and where the swing damage happens.
        private void AnimationEventTriggered(tk2dSpriteAnimator animator, tk2dSpriteAnimationClip clip, int frameIdx)
        {
            // This event happens near the beginning of the shoot animation, and only happens once per combo.
            if (clip.GetFrame(frameIdx).eventInfo == "removeClicked")
            {
                // This part plays the audio for the initial swing, with a quarter second delay to make it match the animation.
                int num = UnityEngine.Random.Range(1, 4);
                StartCoroutine(PostEventDelayed($"Play_spccleaver_swing_00" + num, 0.25f));

                // Set clicked to false so that the initial shot click doesn't count towards your combo.
                clicked = false;
            }

            // This event happens every time the gun's intro gets played, which happens in a load of different scenarios.
            // All it does is clear the cooldown on the gun so that you can fire it again.
            if (clip.GetFrame(frameIdx).eventInfo == "refreshCooldown")
            {
                gun.ClearCooldowns();
            }

            // This event occurs every time a swing happens in the animation.
            // This handles a lot of the gun's function.
            if (clip.GetFrame(frameIdx).eventInfo == "clickCheck")
            {
                if (clicked == false || gun.CurrentOwner.CurrentGun != gun) // This happens if you have NOT clicked since last click check, or if you aren't actually holding the gun.
                {
                    // Checks if an enemy is currently being targeted, if it is then unstun it and make it able to be affected by knockback.
                    // This is done because when you dash and hit an enemy, it gets stunned and immune to knockback.
                    // This code makes it so that when you finish your combo early, it's no longer stunned and gets it's knockback resistance removed.
                    if (highlightedEnemy != null)
                    {
                        if (highlightedEnemy.behaviorSpeculator && highlightedEnemy.behaviorSpeculator.IsStunned) highlightedEnemy.behaviorSpeculator.EndStun();
                        if (highlightedEnemy.knockbackDoer) highlightedEnemy.knockbackDoer.SetImmobile(false, "combo");
                    }
                    DeselectEnemy(); // Deselect the enemy, because if you finish your combo early the enemy shouldn't still be selected.

                    gun.spriteAnimator.Play(gun.introAnimation); // Play the gun's intro animation. This is done for 2 reasons:
                                                                 // 1. It makes the animations look nice. The way I designed the animations make it so that after every click check, the intro animation will look smooth.
                                                                 // 2. It calls the refreshCooldown event, which, when handled, allows you to shoot the cannonball projectile again to target another enemy.
                }
                else // This happens if you DID click since the last click check.
                {
                    bool slashedEnemy = false; // This variable keeps track of whether or not you actually dealt damage to the enemy with this swing.
                                               // This is used to determine which swing sound to play, the standard sound or the impact sound.

                    clicked = false; // Sets clicked back to false, so you have to click again before the next click check.
                    
                    if (highlightedEnemy != null && !highlightedEnemy.healthHaver.IsDead) // The following code only happens if you have an enemy selected, meaning you hit it with your projectile.
                    {
                        if (assaultLevel == -1) // This happens when your assault level is -1, meaning you haven't yet dashed at the enemy.
                        {
                            // Make the player immune to damage for 1 second to avoid unfair damage during dash
                            gun.CurrentOwner.healthHaver.TriggerInvulnerabilityPeriod(1f);

                            // Stun the enemy for 1 second.
                            if (highlightedEnemy.behaviorSpeculator)
                            {
                                highlightedEnemy.behaviorSpeculator.Stun(1f);
                            }
                            
                            // Make the enemy immune to knockback so it doesnt get pushed away during the dash, while other enemies do
                            if (highlightedEnemy.knockbackDoer)
                            {
                                highlightedEnemy.knockbackDoer.SetImmobile(true, "combo");
                            }

                            // time to dash
                            StartCoroutine(LaunchPlayer(gun.CurrentOwner as PlayerController, highlightedEnemy));
                            
                            assaultLevel = 1; // Sets the assault level to 1, indicating that you have dashed and that your combo is now a combo of 1.
                        }
                        else // This code happens if you HAVE already dashed at the enemy.
                        {
                            float damage = 1.5f * assaultLevel; // Damage calculation. The higher your assault level (combo), the higher the damage.
                            
                            // If the player has the Combo King synergy, double the damage of the combo swing
                            if ((gun.CurrentOwner as PlayerController).PlayerHasActiveSynergy("Combo King"))
                            {
                                damage *= 2f;
                            }

                            // This code only occurs if you're close enough the the target enemy to actually hit it with a melee swing.
                            // This is so that you can't target an enemy, move away, then start meleeing other enemies to get a huge combo. you instead have to target and kill one enemy first
                            if ((highlightedEnemy.CenterPosition - gun.CurrentOwner.CenterPosition).magnitude < 3f)
                            {
                                // Make the enemy's skull above their head shake a little bit.
                                // (This skull is created upon the selection of an enemy)
                                var sc = highlightedEnemy.GetComponent<SkullController>();
                                if (sc) sc.shakeAmount = 0.25f;

                                // This happens if the targeted enemy's health is less than the damage you would deal to it.
                                if (highlightedEnemy.healthHaver.GetCurrentHealth() <= damage)
                                {
                                    if (sc) sc.BigSkullAndCrossbones(); // Activate the big skull and crossbones 3D death effect.

                                    bool hasCannonballSynergy = (gun.CurrentOwner as PlayerController).PlayerHasActiveSynergy("Cannons Away");

                                    // Fire 12 decent damage cannonballs, or 24 if the player has the cannons away synergy
                                    int cannonballAmount = (hasCannonballSynergy ? 24 : 12);
                                    for (int i = 0; i < cannonballAmount; i++)
                                    {
                                        GameObject gameObject = SpawnManager.SpawnProjectile(gun.DefaultModule.projectiles[0].gameObject, highlightedEnemy.CenterPosition, Quaternion.Euler(0f, 0f, i * (360f/(float)cannonballAmount)), true);
                                        Projectile component = gameObject.GetComponent<Projectile>();
                                        component.Owner = gun.CurrentOwner;
                                        component.specRigidbody.RegisterSpecificCollisionException(highlightedEnemy.specRigidbody);
                                        component.baseData.damage *= hasCannonballSynergy ? 5f : 2f; // Deal more damage if the player has the synergy
                                        //component.transform.eulerAngles = new Vector3(0f, 0f, (i * 30f) - 90f);

                                        component.OverrideMotionModule = new SimpleCannonballProjectile(); // Assign the cannonballs a motion module, giving it afterimages and allowing it to speed up.
                                    }
                                    LaunchParticles(0f, 360, 1f, 1f, 3000, 15, 2f); // Fire off a death particle burst

                                    if ((gun.CurrentOwner as PlayerController).PlayerHasActiveSynergy("Axes Don't Reload"))
                                    {
                                        gun.spriteAnimator.Play(gun.introAnimation); // If the player has the Axes Don't Reload synergy, instead of forcing a gun reload, simply play the intro animation again to reset the projectile cooldown.
                                    }
                                    else
                                    {
                                        gun.Reload(); // Force the gun to reload, so you can't instantly shoot another projectile
                                    }
                                }

                                // Deal damage
                                ExploderExtensions.DoRadialDamageNoIFrames(damage, gun.CurrentOwner.CenterPosition, 3f, false, true);

                                // If the gun isn't reloading (aka if the enemy didn't just die), launch particles in the direction of the slash
                                if (!gun.IsReloading) LaunchParticles((highlightedEnemy.CenterPosition - gun.CurrentOwner.CenterPosition).ToAngle() - 40f + (50f * (frameIdx == 10 ? 1f : -1f) * ((gun.CurrentAngle > -90f || gun.CurrentAngle < 90f) ? 1f : -1f)));
                                
                                slashedEnemy = true; // Indicate that the impact sound should be played rather than the standard swing sound
                                assaultLevel++; // Increase the combo

                                // Stun the enemy for an additional half-second so it doesn't start shooting at you mid-combo
                                if (highlightedEnemy.behaviorSpeculator)
                                {
                                    highlightedEnemy.behaviorSpeculator.Stun(0.5f);
                                }
                            }
                        }
                    }

                    // Play the swing sound or the impact sound depending on the value of the slashedEnemy variable after a fifth of a second delay to sync with the animation
                    int num = UnityEngine.Random.Range(1, 4);
                    StartCoroutine(PostEventDelayed($"Play_spccleaver_{(slashedEnemy ? "impact" : "swing")}_00" + num, 0.2f));
                    
                }
            }
        }

        // A little coroutine method I made that posts an AkSoundEngine event after a delay
        IEnumerator PostEventDelayed(string eventName, float delay)
        {
            var timer = 0f;
            while (timer < delay)
            {
                timer += BraveTime.DeltaTime;
                yield return null;
            }
            AkSoundEngine.PostEvent(eventName, gun.gameObject);
        }

        // This method handles all the custom particles using unity's particle system.
        // I'm not going to annotate everything in here as it's mostly just assigning values to make the particles look correct. I'm only going to annotate the important things
        // If you're planning on using unity's particle system I recommend messing around with it in unity first to see your changes in real time, then simply copying the values into your code. it'll save you a bunch of time
        void LaunchParticles(float angle, float arc = 70, float bMin = 0.4f, float bMax = 0.75f, int particles = 1000, float speed = 50f, float lifetime = 0.5f)
        {
            // This creates an empty gameobject and sets it to the enemy's center position
            var pso = new GameObject("privateer's cleaver particle system");
            pso.transform.position = highlightedEnemy.CenterPosition;
            pso.transform.localRotation = Quaternion.Euler(0f, 0f, angle);

            // PARTICLE SYSTEM SECTION --
            var ps = pso.GetOrAddComponent<ParticleSystem>();

            var main = ps.main;
            main.maxParticles = 10000;
            main.playOnAwake = false;
            main.duration = 0.1f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.1f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 2.2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color32(100,100,100,255), new Color32(0, 0, 0, 255));
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = particles;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.2f;
            shape.arc = arc;

            var vOL = ps.velocityOverLifetime;
            vOL.enabled = true;
            vOL.speedModifier = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)));

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var brightness = UnityEngine.Random.Range(bMin, bMax);
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.black, 0.5f) }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0.7f, 1f), new Keyframe(1f, 0f)));

            var particleRenderer = ps.gameObject.GetComponent<ParticleSystemRenderer>();
            particleRenderer.material = new Material(Shader.Find("Sprites/Default"));
            particleRenderer.material.mainTexture = particleTexture; // Give the particle the particle texture that was loaded earlier
            // -- END OF PARTICLE SYSTEM SECTION

            ps.Play(); // Play the particle system

            Destroy(pso, main.duration + 3f); // Destroy the particle system after 3 seconds plus however long it's duration is to allow all the particles to fully disappear
        }

        // This is the code for the actual player dash.
        IEnumerator LaunchPlayer(PlayerController player, AIActor enemy)
        {
            player.SetIsFlying(true, "DASHIN", false); // Make the player fly so they can cross over pits while they dash.

            var targetPosition = enemy.transform.position; // This is set to the target dash destination

            // This part sets up black afterimages on the player, for a small added effect.
            var aitc = player.gameObject.GetOrAddComponent<AfterImageTrailController>();
            aitc.dashColor = new Color32(0, 0, 0, 255);
            aitc.shadowTimeDelay = 0.05f;
            aitc.spawnShadows = true;

            int time = 20; // This is how many frames it'll take to travel the dash distance

            // If the player has the destination locked synergy, make the dash happen in 3 frames instead of 20
            if (player.PlayerHasActiveSynergy("Destination Locked"))
            {
                time = 3;
            }

            for (int i = 0; i < time; i++)
            {
                player.specRigidbody.Velocity += (Vector2)(targetPosition - player.transform.position) * 100f * (1f / (float)time); // Add to the players velocity so that they'll be at the location after the amount of frames using math™
                Exploder.DoRadialKnockback(player.CenterPosition, 50f, 3f); // Deal knockback so that other enemies are pushed away. The target enemy got applied with knockback resistance so it's not affected by this
                yield return null;
            }

            player.SetIsFlying(false, "DASHIN", false); // Remove flight from the player now that the dash is over
            aitc.spawnShadows = false; // Disable the player afterimages
            yield break;
        }

        void Update()
        {
            if (!this.gun.PreventNormalFireAudio) // Make it so the gun doesn't make any regular sound
            {
                this.gun.PreventNormalFireAudio = true;
            }

            // This happens if the player has the gun held.
            if (gun.CurrentOwner is PlayerController owner)
            {
                // If the player has the Hyperspeed Swing synergy, set their gun's fps to 30. Otherwise set it back to 20
                bool hasSynergy = owner.PlayerHasActiveSynergy("Hyperspeed Swing");
                int fps = (int)gun.spriteAnimator.GetClipByName(gun.shootAnimation).fps;
                if (fps == 20 && hasSynergy)
                {
                    GunExt.SetAnimationFPS(gun, 30);
                }
                else if (fps == 30 && !hasSynergy)
                {
                    GunExt.SetAnimationFPS(gun, 20);
                }

                // This section checks to see if the shoot button was pressed, and if it was, sets the clicked variable to true
                var input = BraveInput.GetInstanceForPlayer(owner.PlayerIDX);
                if (input.ActiveActions.ShootAction.State == true && input.ActiveActions.ShootAction.LastState == false)
                {
                    clicked = true;
                }
                if (gun.IsReloading) // If the gun is reloading (aka just combo killed an enemy), set clicked to false so the shooting animation stops
                {
                    clicked = false;
                }
                
                // This code checks to see if an enemy is targeted, and if it is, give it a bright white outline.
                if (highlightedEnemy != null)
                {
                    Material outlineMaterial = SpriteOutlineManager.GetOutlineMaterial(highlightedEnemy.sprite);
                    if (outlineMaterial != null)
                    {
                        outlineMaterial.SetColor("_OverrideColor", new Color(60f, 60f, 60f));
                    }
                }
            }
            else
            {
                DeselectEnemy(); // If the gun isn't held by the player, deselect an enemy if one is selected.
            }
        }

        public override void PostProcessProjectile(Projectile projectile)
        {
            DeselectEnemy(); // If an enemy is selected, deselect it when a projectile is shot
            projectile.IgnoreTileCollisionsFor(0.6f); // Make it go through walls to allow you to shoot with your back to a wall (since the speed actually starts out negative)
            projectile.OverrideMotionModule = new CannonballProjectile(); // Give it a custom motion module. This handles all the movement of the cannonball.

            projectile.OnHitEnemy += OnHitEnemy; // This attaches to OnHitEnemy, which is where enemies get selected.
            projectile.specRigidbody.OnPreRigidbodyCollision += OnPreRigidbodyCollision; // This makes it so that the projectile won't break on minor breakables in a room

            // these 3 lines are probably unneeded but im keeping them in so something doesn't break horribly
            var rot = projectile.Owner.FacingDirection;
            projectile.transform.eulerAngles = new Vector3(0f, 0f, rot);
            projectile.transform.position = projectile.Owner.CenterPosition;

            base.PostProcessProjectile(projectile);
        }

        private void OnPreRigidbodyCollision(SpeculativeRigidbody myRigidbody, PixelCollider myPixelCollider, SpeculativeRigidbody otherRigidbody, PixelCollider otherPixelCollider)
        {
            if (otherRigidbody.aiActor == null || (otherRigidbody.healthHaver != null && otherRigidbody.healthHaver.IsDead)) PhysicsEngine.SkipCollision = true; // If the projectile collides with something that isn't an enemy, ignore it so that the projectile doesn't break. Also do this is the enemy is dead
        }

        // The select enemy method. This gets called when an enemy is hit with a regular projectile.
        void SelectEnemy(AIActor target)
        {
            DeselectEnemy(); // Deselect the current enemy if one is selected.

            // Give it a nice white outline.
            Material outlineMaterial = SpriteOutlineManager.GetOutlineMaterial(target.sprite);
            if (outlineMaterial != null)
            {
                outlineMaterial.SetColor("_OverrideColor", new Color(60f, 60f, 60f));
            }

            CreateOminousSkull(target); // Creates the damage indicator skull above the enemy's head
            highlightedEnemy = target; // Sets the targeted enemy variable
        }
        
        void DeselectEnemy()
        {
            assaultLevel = -1; // Set your assault level back to -1 (meaning pre-dashed) since if theres no selected enemy there should be no combo
            if (highlightedEnemy != null) // This only happens if an enemy is currently selected
            {
                // Set it's outline back to black
                Material outlineMaterial = SpriteOutlineManager.GetOutlineMaterial(highlightedEnemy.sprite);
                if (outlineMaterial != null)
                {
                    outlineMaterial.SetColor("_OverrideColor", new Color(0f, 0f, 0f));
                }

                // Fade out the damage indicator skull if the enemy has one
                var skull = highlightedEnemy.GetComponent<SkullController>();
                if (skull != null) skull.FadeOut();

                highlightedEnemy = null; // Set the targeted enemy variable to null
            }
        }

        // When the main projectile hits an enemy, target it
        private void OnHitEnemy(Projectile arg1, SpeculativeRigidbody arg2, bool arg3)
        {
            SelectEnemy(arg2.aiActor);
        }

        // This is the motion module for the simple cannonball projetiles. These are the ones that explode from an enemy when it dies from a combo
        // This code is a little wack since I wrote it on 24+ hours without sleep, but it works, so
        public class SimpleCannonballProjectile : ProjectileMotionModule
        {
            public override void UpdateDataOnBounce(float angleDiff) { } // Unused

            protected bool setup = false; // A simple variable that checks to see if I've set certain things up in the projectile
            protected float initialRot = 0f; // A variable that gets set during setup that determines what angle the projectile is going

            // This method happens every frame and determines how the projectile will move
            public override void Move(Projectile source, Transform projectileTransform, tk2dBaseSprite projectileSprite, SpeculativeRigidbody specRigidbody, ref float m_timeElapsed, ref Vector2 m_currentDirection, bool Inverted, bool shouldRotate)
            {
                // This occurs once in the projectile's lifetime, right at the beginning.
                if (!setup)
                {
                    setup = true; // Indicate that things have been set up so it won't do the stuff again
                    initialRot = m_currentDirection.Rotate(-90f).ToAngle(); // Assign a value to initialRot
                    source.sprite.renderer.enabled = true; // ???? im entirely blanking on why I put this here, it's probably unneeded since the renderer should already be enabled
                    if (!(this is CannonballProjectile)) source.Speed = -1f; // hack-ish way to set the speed to a fixed value if it isn't a complex cannonball projectile, which inherits from this class

                    // Set up black afterimages on the projectile
                    var aitc = source.sprite.gameObject.AddComponent<AfterImageTrailController>();
                    aitc.dashColor = new Color32(0, 0, 0, 255);
                    aitc.shadowLifetime = 0.2f;
                    aitc.shadowTimeDelay = 0.05f;
                }

                // This code makes the projectile fade in from black as it fires
                var opac = Mathf.Clamp01(m_timeElapsed * 1.5f);
                source.ChangeColor(0f, new Color(opac, opac, opac));
                
                source.transform.eulerAngles = new Vector3(0f, 0f, initialRot); // Make sure the rotation is correct

                source.specRigidbody.Velocity = m_currentDirection * source.Speed; // Update the projectile's velocity so it actually, yknow, moves

                m_timeElapsed += (BraveTime.DeltaTime); // Increase the timeElapsed variable to keep track of how long the projectile has been alive
                source.Speed += BraveTime.DeltaTime * 15f; // Increase the projectile's speed
            }
        }

        // This inherits from the SimpleCannonballProjectile. It's the code for the main projectile the weapon fires
        // this code is also a little wack for the same reason
        public class CannonballProjectile : SimpleCannonballProjectile
        {
            public override void Move(Projectile source, Transform projectileTransform, tk2dBaseSprite projectileSprite, SpeculativeRigidbody specRigidbody, ref float m_timeElapsed, ref Vector2 m_currentDirection, bool Inverted, bool shouldRotate)
            {
                base.Move(source, projectileTransform, projectileSprite, specRigidbody, ref m_timeElapsed, ref m_currentDirection, Inverted, shouldRotate); // Call the base method on the SimpleCannonballProjectile
                
                if (m_timeElapsed < 0.33333333333f) // This occurs if the projectile is early in it's lifetime
                {
                    // Rotate the projectile so it is up to date with your new aim direction.
                    // This is to make it so that even if you move your cursor the projectile will still go in the right direction (since it actually initially goes backwards and gains speed)
                    var newDir = source.Owner.FacingDirection - 90f;
                    var diff = newDir - initialRot;
                    initialRot = newDir;
                    m_currentDirection = m_currentDirection.Rotate(diff);

                    source.specRigidbody.Velocity += source.Owner.Velocity; // This does the same thing as the above code except for the velocity, so if you walk around it'll move with you
                }
                if (m_timeElapsed > 0.75f)
                {
                    //specRigidbody.CollideWithOthers = true;
                }

                source.Speed += BraveTime.DeltaTime * 30f; // Increase the speed, more than the base method increases it
            }
        }

        // 3D TIME WOOOOOOOOOO
        // This method creates the fancy rotating 3D skull damage indicator
        void CreateOminousSkull(AIActor enemy)
        {
            var skullContainer = enemy.gameObject.AddChild("ominous skull"); // Add a child to the enemy

            // Set up a bunch of position stuff
            skullContainer.transform.position = enemy.transform.position;
            skullContainer.transform.localScale = new Vector3(0.008f, 0.008f, 0.008f);
            skullContainer.transform.localRotation = Quaternion.Euler(90f, 90f, 0f);
            skullContainer.transform.position = enemy.sprite.WorldTopCenter;
            skullContainer.transform.localPosition = new Vector3(0f, 0f, -10f);

            skullContainer.SetLayerRecursively(LayerMask.NameToLayer("Unpixelated")); // Set the layer to unpixelated so it doesn't look awful

            // Add a meshrenderer. This is required to actually show the mesh
            MeshRenderer renderer = skullContainer.AddComponent<MeshRenderer>();
            renderer.material = new Material(Game.Items["orange"].GetComponent<MeshRenderer>().material.shader); // Steal the orange's shader
            renderer.material.mainTexture = skullTexture; // Set the texture of the skull to the one we loaded earlier

            // Add a meshfilter. This is also required, since it actually contains the mesh data
            MeshFilter filter = skullContainer.AddComponent<MeshFilter>();
            filter.mesh = GunJam1Module.skullMesh; // Set the mesh to the skull we loaded when our mod got loaded

            // Add and set up the skull controller. This is what controls the skull's movement, rotation, and shaking
            var sc = enemy.gameObject.AddComponent<SkullController>();
            sc.skull = skullContainer;
        }

        // This class handles all of the 3D skull damage indicator's behavior.
        public class SkullController : BraveBehaviour
        {
            public GameObject skull; // This is set to the gameobject that has the meshrenderer for the skull on it.

            float maxScale = 0.01f; // This variable determines how big the skull can be at it's max
            float scale = 0f; // This is how big the skull actually is currently
            Vector3 rot = new Vector3(90f, 90f, 0f); // This is the skull's current rotation

            public float shakeAmount = 0f; // The shake amount of the skull

            void Update()
            {
                float targetScale = Mathf.Lerp(0f, maxScale, healthHaver.GetCurrentHealthPercentage()); // Gets the desired scale value that the skull should be
                scale += (targetScale - scale) * BraveTime.DeltaTime * 10f; // Adds to the scale so it'll smoothly get closer to the desired amount using math™
                if (scale < 0.0001f && maxScale < 0.0001f) // If the scale is 0 and the max scale is 0, destroy the skull. This is what causes it to get destroyed after fading out.
                {
                    Destroy(this);
                    return;
                }

                skull.transform.localScale = new Vector3(scale, scale, scale); // Actually set the scale on the skull object

                Vector3 targetRot = new Vector3(0f, 180f, 0f); // The desired rotation of the skull. This makes it face directly towards the camera
                rot += (targetRot - rot) * BraveTime.DeltaTime * 6f; // Adds to the rotation so it'll smoothly get closer to the desired amount using math™

                float rotSpeed = 3f; // This variable changes how fast the skull does it's sin rotation

                // Actually set the rotation of the skull, and adds a new vector with sin values that have different frequencies and magnitudes for that s m o o t h random-looking rotation
                skull.transform.localRotation = Quaternion.Euler(rot + new Vector3((float)Math.Sin(BraveTime.ScaledTimeSinceStartup * rotSpeed) * 15f, (float)Math.Sin(BraveTime.ScaledTimeSinceStartup*1.64f * rotSpeed) * 10f, (float)Math.Sin(BraveTime.ScaledTimeSinceStartup * 1.22f * rotSpeed) * 25f));

                if (shakeAmount > 0) shakeAmount -= BraveTime.DeltaTime; // If the shake amount is higher than 0, remove a little bit from it

                // Sets the position of the skull equal to the top of the actor's sprite plus a random value determined by the shake amount
                skull.transform.position = new Vector2(aiActor.sprite.WorldTopCenter.x + UnityEngine.Random.Range(shakeAmount * -1f, shakeAmount), aiActor.sprite.WorldTopCenter.y + 1.25f + UnityEngine.Random.Range(shakeAmount * -1f, shakeAmount));
            }

            // This method just sets the max scale to 0, so that it'll smoothly fade out then delete itself
            // This is called when an enemy gets deselected
            public void FadeOut()
            {
                maxScale = 0f;
            }
            
            // big code to set up the death effect that literally lasts for 1 second why the fuck did i make this
            // This is called when an enemy dies from a combo swing
            public void BigSkullAndCrossbones()
            {
                // Create the gameobject of the effect and set it to the center of the enemy
                var sAc = new GameObject("skull and crossbones death marker");
                sAc.transform.position = aiActor.sprite.WorldCenter;

                // Create the skull gameobject as a child of the effect gameobject and set up it's transform
                var skull = sAc.gameObject.AddChild("ominous skull");
                skull.transform.localScale = new Vector3(0f, 0f, 0f);
                skull.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
                skull.transform.localPosition = new Vector3(0f, 0f, 0f);

                // Create the tlbr (top left to bottom right) bone gameobject as a child of the effect gameobject and set up it's transform
                var tlbrBone = sAc.gameObject.AddChild("top left to bottom right bone");
                tlbrBone.transform.localScale = new Vector3(0f, 0f, 0f);
                tlbrBone.transform.localRotation = Quaternion.Euler(0f, -180f, -90f);
                tlbrBone.transform.localPosition = new Vector3(0f, 0f, 0f);

                // Create the bltr (bottom left to top right) bone gameobject as a child of the effect gameobject and set up it's transform
                var bltrBone = sAc.gameObject.AddChild("bottom left to top right bone");
                bltrBone.transform.localScale = new Vector3(0f, 0f, 0f);
                bltrBone.transform.localRotation = Quaternion.Euler(0f, -180f, 90f);
                bltrBone.transform.localPosition = new Vector3(0f, 0f, 0f);

                // Sets the whole effect's gameobject to unpixelated so it doesnt look like garbage
                sAc.SetLayerRecursively(LayerMask.NameToLayer("Unpixelated"));

                var shader = Game.Items["orange"].GetComponent<MeshRenderer>().material.shader; // Steal the shader from the orange

                // Set up the meshrenderer, texture, and meshfilter for the skull.
                MeshRenderer srenderer = skull.AddComponent<MeshRenderer>();
                srenderer.material = new Material(shader);
                srenderer.material.mainTexture = skullTexture;
                MeshFilter sfilter = skull.AddComponent<MeshFilter>();
                sfilter.mesh = GunJam1Module.skullMesh;

                // Set up the meshrenderer, texture, and meshfilter for the tlbr bone.
                MeshRenderer b1renderer = tlbrBone.AddComponent<MeshRenderer>();
                b1renderer.material = new Material(shader);
                b1renderer.material.mainTexture = boneTexture;
                MeshFilter b1filter = tlbrBone.AddComponent<MeshFilter>();
                b1filter.mesh = GunJam1Module.boneMesh;

                // Set up the meshrenderer, texture, and meshfilter for the bltr bone.
                MeshRenderer b2renderer = bltrBone.AddComponent<MeshRenderer>();
                b2renderer.material = new Material(shader);
                b2renderer.material.mainTexture = boneTexture;
                MeshFilter b2filter = bltrBone.AddComponent<MeshFilter>();
                b2filter.mesh = GunJam1Module.boneMesh;

                // Add a skull and crossbones controller to the effect object and set up it's values, this is what controls the movement of the death effect
                var sc = sAc.AddComponent<SkullAndCrossbonesController>();
                sc.skull = skull;
                sc.tlbrBone = tlbrBone;
                sc.bltrBone = bltrBone;
                sc.deathPosition = aiActor.sprite.WorldCenter;

                sc.Invoke("FadeOut", 1f); // Cause the effect to fade out after literally 1 second. i really dont know why i made this, i had to find a whole new bone mesh and fine-tune all the values so it looked right, then make a texture for it, set it up in unity first, modify the loader code to load that mesh, then make a solid 150 lines of for a death effect that lasts ONE SECOND
            }

            // Destroy the actual skull object when the behavior gets destroyed
            protected override void OnDestroy()
            {
                Destroy(skull);
                base.OnDestroy();
            }
        }

        // This class handles the behavior of the skull and crossbones death effect
        public class SkullAndCrossbonesController : BraveBehaviour
        {
            public GameObject skull; // The skull gameobject
            public GameObject tlbrBone; // The tlbrbone gameobject
            public GameObject bltrBone; // the bltrbone gameobject

            public Vector2 deathPosition; // This variable is set to the position of the enemy's death, so the effect is placed in the right spot
            
            float targetScale = 0.015f; // The target scale of the skull and crossbones
            float maxScale = 0.015f; // The max scale of the effect. This is pretty much always identical to target scale, and is used to setup the proper scale for the bones
            float scale = 0f; // The actual scale of the skull and crossbones

            Vector3 skullRot = new Vector3(0f, -180f, 0f); // The skull's current rotation
            Vector3 rot = new Vector3(0f, -180f, -90f); // The tlbr bone's current rotation
            Vector3 rot2 = new Vector3(0f, -180f, 90f); // The bltr bone's current rotation

            void Update()
            {
                scale += (targetScale - scale) * BraveTime.DeltaTime * 10f; // Adds to the scale so it'll smoothly get closer to the desired amount using math™
                if (scale < 0.0001f && targetScale < 0.0001f) // If the scale is 0 and the target scale is 0, destroy the effect. This is what causes it to get destroyed after fading out.
                {
                    Destroy(this);
                    return;
                }

                // stupid math I had to do to get the bones to have a proper scale
                float boneScaleMulti = 0.12f * 1.5f / maxScale;
                float boneXScaleMulti = 0.09f * 1.5f / maxScale;

                // Sets all the objects to the proper scale
                skull.transform.localScale = new Vector3(scale, scale, scale);
                tlbrBone.transform.localScale = new Vector3(scale * boneXScaleMulti, scale * boneScaleMulti, scale * boneScaleMulti);
                bltrBone.transform.localScale = new Vector3(scale * boneXScaleMulti, scale * boneScaleMulti, scale * boneScaleMulti);

                // Same thing as the regular skull controller's rotation handling, but this time for the tlbr bone
                Vector3 targetRot = new Vector3(0f, 0f, 52f);
                rot += (targetRot - rot) * BraveTime.DeltaTime * 6f;
                tlbrBone.transform.localRotation = Quaternion.Euler(rot);

                // Same thing as the regular skull controller's rotation handling, but this time for the bltr bone
                targetRot = new Vector3(0f, 0f, -52f);
                rot2 += (targetRot - rot2) * BraveTime.DeltaTime * 6f;
                bltrBone.transform.localRotation = Quaternion.Euler(rot2);

                // Same thing as the regular skull controller's rotation handling
                targetRot = new Vector3(0f, 0f, 0f);
                skullRot += (targetRot - skullRot) * BraveTime.DeltaTime * 6f;
                skull.transform.localRotation = Quaternion.Euler(skullRot);

                // Set the objects to the proper position. The z values are for layering, to make sure the skull appears on top of the bones
                skull.transform.position = new Vector3(deathPosition.x, deathPosition.y, -5f);
                tlbrBone.transform.position = new Vector3(deathPosition.x, deathPosition.y, -1f);
                bltrBone.transform.position = new Vector3(deathPosition.x, deathPosition.y, -1f);
            }

            // This method just sets the max scale to 0, so that it'll smoothly fade out then delete itself
            // This is called after 1 second
            public void FadeOut()
            {
                targetScale = 0f;
            }

            // When destroyed, destroy all the mesh objects aswell
            protected override void OnDestroy()
            {
                Destroy(skull);
                Destroy(tlbrBone);
                Destroy(bltrBone);
                base.OnDestroy();
            }
        }
    }
}

// I FINISHED ANNOTATING THIS
// WOOHOO