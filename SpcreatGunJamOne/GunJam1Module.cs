using ItemAPI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace SpcreatGunJamOne
{
    public class GunJam1Module : ETGModule
    {
        // Used in to load audio
        public static string ZipFilePath;
        public static string FilePath;

        // Meshes for the 3D skull and bone. These are loaded in during the audiobank loading section.
        // See resourceLoaderSoundbanks.cs lines 73 and 127
        public static Mesh skullMesh;
        public static Mesh boneMesh;

        public override void Init()
        {
            // Assign the filepath so the soundbank loader knows where to look, then load the soundbank (and the 3D meshes)
            ZipFilePath = this.Metadata.Archive;
            FilePath = this.Metadata.Directory;
            AudioResourceLoader.InitAudio();
        }

        public override void Start()
        {
            try
            {
                // Initialize ItemAPI
                FakePrefabHooks.Init();
                ItemBuilder.Init();

                // Add the cleaver
                PrivateersCleaver.Init();

                // Set up the synergies for the gun
                CustomSynergies.Add("Cannons Away", new List<string> { "spc:privateer_cleaver", "serious_cannon" }, null, false);
                CustomSynergies.Add("Combo King", new List<string> { "spc:privateer_cleaver" }, new List<string> { "katana_bullets", "knife_shield", "boxing_glove", "casey", "excaliber", "fightsabre", "huntsman", "wood_beam" }, false);
                CustomSynergies.Add("Destination Locked", new List<string> { "spc:privateer_cleaver", "corsair" }, null, false);
                CustomSynergies.Add("Hyperspeed Swing", new List<string> { "spc:privateer_cleaver" }, new List<string> { "gunboots", "bionic_leg", "shotga_cola", "shotgun_coffee", "ballistic_boots" }, false);
                CustomSynergies.Add("Axes Don't Reload", new List<string> { "spc:privateer_cleaver", "cog_of_battle" }, null, false);

                ETGModConsole.Log("Privateer's Cleaver has successfully loaded.");
            }
            catch (Exception e)
            {
                ETGModConsole.Log("Privateer's Cleaver has failed to load! Please message spcreat with the following message:\n" + e);
            }
        }

        public override void Exit() { } // Unused
    }

    // This is an exact clone of GunBehaviour except the Start method is virtual, which lets me override it in my gun.
    public class BetterGunBehaviour : MonoBehaviour
    {
        public virtual void Start()
        {
            this.gun = base.GetComponent<Gun>();
            Gun gun = this.gun;
            gun.OnInitializedWithOwner = (Action<GameActor>)Delegate.Combine(gun.OnInitializedWithOwner, new Action<GameActor>(this.OnInitializedWithOwner));
            Gun gun2 = this.gun;
            gun2.PostProcessProjectile = (Action<Projectile>)Delegate.Combine(gun2.PostProcessProjectile, new Action<Projectile>(this.PostProcessProjectile));
            Gun gun3 = this.gun;
            gun3.OnDropped = (Action)Delegate.Combine(gun3.OnDropped, new Action(this.OnDropped));
            Gun gun4 = this.gun;
            gun4.OnAutoReload = (Action<PlayerController, Gun>)Delegate.Combine(gun4.OnAutoReload, new Action<PlayerController, Gun>(this.OnAutoReload));
            Gun gun5 = this.gun;
            gun5.OnReloadPressed = (Action<PlayerController, Gun, bool>)Delegate.Combine(gun5.OnReloadPressed, new Action<PlayerController, Gun, bool>(this.OnReloadPressed));
            Gun gun6 = this.gun;
            gun6.OnFinishAttack = (Action<PlayerController, Gun>)Delegate.Combine(gun6.OnFinishAttack, new Action<PlayerController, Gun>(this.OnFinishAttack));
            Gun gun7 = this.gun;
            gun7.OnPostFired = (Action<PlayerController, Gun>)Delegate.Combine(gun7.OnPostFired, new Action<PlayerController, Gun>(this.OnPostFired));
            Gun gun8 = this.gun;
            gun8.OnAmmoChanged = (Action<PlayerController, Gun>)Delegate.Combine(gun8.OnAmmoChanged, new Action<PlayerController, Gun>(this.OnAmmoChanged));
        }

        public virtual void OnInitializedWithOwner(GameActor actor)
        {
        }

        public virtual void PostProcessProjectile(Projectile projectile)
        {
        }

        public virtual void OnDropped()
        {
        }

        public virtual void OnAutoReload(PlayerController player, Gun gun)
        {
        }

        public virtual void OnReloadPressed(PlayerController player, Gun gun, bool bSOMETHING)
        {
        }

        public virtual void OnFinishAttack(PlayerController player, Gun gun)
        {
        }

        public virtual void OnPostFired(PlayerController player, Gun gun)
        {
        }

        public virtual void OnAmmoChanged(PlayerController player, Gun gun)
        {
        }

        public virtual Projectile OnPreFireProjectileModifier(Gun gun, Projectile projectile)
        {
            return projectile.EnabledClonedPrefab();
        }

        public BetterGunBehaviour()
        {
        }

        protected Gun gun;
    }

    // This is what I use to deal damage since I don't want to bother with real melee.
    public static class ExploderExtensions
    {
        // An exact clone of Exploder.DoRadialDamage, except it ignores the enemy invulnerability frames since the original didn't allow for that.
        public static void DoRadialDamageNoIFrames(float damage, Vector3 position, float radius, bool damagePlayers, bool damageEnemies, bool ignoreDamageCaps = false, VFXPool hitVFX = null)
        {
            List<HealthHaver> allHealthHavers = StaticReferenceManager.AllHealthHavers;
            if (allHealthHavers != null)
            {
                for (int i = 0; i < allHealthHavers.Count; i++)
                {
                    HealthHaver healthHaver = allHealthHavers[i];
                    if (healthHaver)
                    {
                        if (healthHaver.gameObject.activeSelf)
                        {
                            if (!healthHaver.aiActor || !healthHaver.aiActor.IsGone)
                            {
                                if (!healthHaver.aiActor || healthHaver.aiActor.isActiveAndEnabled)
                                {
                                    for (int j = 0; j < healthHaver.NumBodyRigidbodies; j++)
                                    {
                                        SpeculativeRigidbody bodyRigidbody = healthHaver.GetBodyRigidbody(j);
                                        Vector2 a = healthHaver.transform.position.XY();
                                        Vector2 vector = a - position.XY();
                                        bool flag = false;
                                        bool flag2 = false;
                                        float num;
                                        if (bodyRigidbody.HitboxPixelCollider != null)
                                        {
                                            a = bodyRigidbody.HitboxPixelCollider.UnitCenter;
                                            vector = a - position.XY();
                                            num = BraveMathCollege.DistToRectangle(position.XY(), bodyRigidbody.HitboxPixelCollider.UnitBottomLeft, bodyRigidbody.HitboxPixelCollider.UnitDimensions);
                                        }
                                        else
                                        {
                                            a = healthHaver.transform.position.XY();
                                            vector = a - position.XY();
                                            num = vector.magnitude;
                                        }
                                        if (num < radius)
                                        {
                                            PlayerController component = healthHaver.GetComponent<PlayerController>();
                                            if (component != null)
                                            {
                                                bool flag3 = true;
                                                if (PassiveItem.ActiveFlagItems.ContainsKey(component) && PassiveItem.ActiveFlagItems[component].ContainsKey(typeof(HelmetItem)) && num > radius * HelmetItem.EXPLOSION_RADIUS_MULTIPLIER)
                                                {
                                                    flag3 = false;
                                                }
                                                if (damagePlayers && flag3 && !component.IsEthereal)
                                                {
                                                    HealthHaver healthHaver2 = healthHaver;
                                                    float damage2 = 0.5f;
                                                    Vector2 direction = vector;
                                                    string enemiesString = StringTableManager.GetEnemiesString("#EXPLOSION", -1);
                                                    CoreDamageTypes damageTypes = CoreDamageTypes.None;
                                                    DamageCategory damageCategory = DamageCategory.Normal;
                                                    healthHaver2.ApplyDamage(damage2, direction, enemiesString, damageTypes, damageCategory, false, null, ignoreDamageCaps);
                                                    flag2 = true;
                                                }
                                            }
                                            else if (damageEnemies)
                                            {
                                                AIActor aiActor = healthHaver.aiActor;
                                                if (damagePlayers || !aiActor || aiActor.IsNormalEnemy)
                                                {
                                                    HealthHaver healthHaver3 = healthHaver;
                                                    Vector2 direction = vector;
                                                    string enemiesString = StringTableManager.GetEnemiesString("#EXPLOSION", -1);
                                                    CoreDamageTypes damageTypes = CoreDamageTypes.None;
                                                    DamageCategory damageCategory = DamageCategory.Normal;
                                                    healthHaver3.ApplyDamage(damage, direction, enemiesString, damageTypes, damageCategory, true, null, ignoreDamageCaps);
                                                    flag2 = true;
                                                }
                                            }
                                            flag = true;
                                        }
                                        if (flag2 && hitVFX != null)
                                        {
                                            if (bodyRigidbody.HitboxPixelCollider != null)
                                            {
                                                PixelCollider pixelCollider = bodyRigidbody.GetPixelCollider(ColliderType.HitBox);
                                                Vector2 v = BraveMathCollege.ClosestPointOnRectangle(position, pixelCollider.UnitBottomLeft, pixelCollider.UnitDimensions);
                                                hitVFX.SpawnAtPosition(v, 0f, null, null, null, null, false, null, null, false);
                                            }
                                            else
                                            {
                                                hitVFX.SpawnAtPosition(healthHaver.transform.position.XY(), 0f, null, null, null, null, false, null, null, false);
                                            }
                                        }
                                        if (flag)
                                        {
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    // I found this code here: http://wiki.unity3d.com/index.php?title=ObjImporter
    // It allows me to load a .obj file at runtime, which I use for the skull and bone meshes.
    public class ObjImporter
    {
        private struct meshStruct
        {
            public Vector3[] vertices;
            public Vector3[] normals;
            public Vector2[] uv;
            public Vector2[] uv1;
            public Vector2[] uv2;
            public int[] triangles;
            public int[] faceVerts;
            public int[] faceUVs;
            public Vector3[] faceData;
            public string name;
            public string fileName;
        }

        // Use this for initialization
        public Mesh ImportFile(string filePath)
        {
            meshStruct newMesh = createMeshStruct(filePath);
            populateMeshStruct(ref newMesh);

            Vector3[] newVerts = new Vector3[newMesh.faceData.Length];
            Vector2[] newUVs = new Vector2[newMesh.faceData.Length];
            Vector3[] newNormals = new Vector3[newMesh.faceData.Length];
            int i = 0;
            /* The following foreach loops through the facedata and assigns the appropriate vertex, uv, or normal
             * for the appropriate Unity mesh array.
             */
            foreach (Vector3 v in newMesh.faceData)
            {
                newVerts[i] = newMesh.vertices[(int)v.x - 1];
                if (v.y >= 1)
                    newUVs[i] = newMesh.uv[(int)v.y - 1];

                if (v.z >= 1)
                    newNormals[i] = newMesh.normals[(int)v.z - 1];
                i++;
            }

            Mesh mesh = new Mesh();

            mesh.vertices = newVerts;
            mesh.uv = newUVs;
            mesh.normals = newNormals;
            mesh.triangles = newMesh.triangles;

            mesh.RecalculateBounds();

            return mesh;
        }

        private static meshStruct createMeshStruct(string filename)
        {
            int triangles = 0;
            int vertices = 0;
            int vt = 0;
            int vn = 0;
            int face = 0;
            meshStruct mesh = new meshStruct();
            mesh.fileName = filename;
            StreamReader stream = File.OpenText(filename);
            string entireText = stream.ReadToEnd();
            stream.Close();
            using (StringReader reader = new StringReader(entireText))
            {
                string currentText = reader.ReadLine();
                char[] splitIdentifier = { ' ' };
                string[] brokenString;
                while (currentText != null)
                {
                    if (!currentText.StartsWith("f ") && !currentText.StartsWith("v ") && !currentText.StartsWith("vt ")
                        && !currentText.StartsWith("vn "))
                    {
                        currentText = reader.ReadLine();
                        if (currentText != null)
                        {
                            currentText = currentText.Replace("  ", " ");
                        }
                    }
                    else
                    {
                        currentText = currentText.Trim();                           //Trim the current line
                        brokenString = currentText.Split(splitIdentifier, 50);      //Split the line into an array, separating the original line by blank spaces
                        switch (brokenString[0])
                        {
                            case "v":
                                vertices++;
                                break;
                            case "vt":
                                vt++;
                                break;
                            case "vn":
                                vn++;
                                break;
                            case "f":
                                face = face + brokenString.Length - 1;
                                triangles = triangles + 3 * (brokenString.Length - 2); /*brokenString.Length is 3 or greater since a face must have at least
                                                                                     3 vertices.  For each additional vertice, there is an additional
                                                                                     triangle in the mesh (hence this formula).*/
                                break;
                        }
                        currentText = reader.ReadLine();
                        if (currentText != null)
                        {
                            currentText = currentText.Replace("  ", " ");
                        }
                    }
                }
            }
            mesh.triangles = new int[triangles];
            mesh.vertices = new Vector3[vertices];
            mesh.uv = new Vector2[vt];
            mesh.normals = new Vector3[vn];
            mesh.faceData = new Vector3[face];
            return mesh;
        }

        private static void populateMeshStruct(ref meshStruct mesh)
        {
            StreamReader stream = File.OpenText(mesh.fileName);
            string entireText = stream.ReadToEnd();
            stream.Close();
            using (StringReader reader = new StringReader(entireText))
            {
                string currentText = reader.ReadLine();

                char[] splitIdentifier = { ' ' };
                char[] splitIdentifier2 = { '/' };
                string[] brokenString;
                string[] brokenBrokenString;
                int f = 0;
                int f2 = 0;
                int v = 0;
                int vn = 0;
                int vt = 0;
                int vt1 = 0;
                int vt2 = 0;
                while (currentText != null)
                {
                    if (!currentText.StartsWith("f ") && !currentText.StartsWith("v ") && !currentText.StartsWith("vt ") &&
                        !currentText.StartsWith("vn ") && !currentText.StartsWith("g ") && !currentText.StartsWith("usemtl ") &&
                        !currentText.StartsWith("mtllib ") && !currentText.StartsWith("vt1 ") && !currentText.StartsWith("vt2 ") &&
                        !currentText.StartsWith("vc ") && !currentText.StartsWith("usemap "))
                    {
                        currentText = reader.ReadLine();
                        if (currentText != null)
                        {
                            currentText = currentText.Replace("  ", " ");
                        }
                    }
                    else
                    {
                        currentText = currentText.Trim();
                        brokenString = currentText.Split(splitIdentifier, 50);
                        switch (brokenString[0])
                        {
                            case "g":
                                break;
                            case "usemtl":
                                break;
                            case "usemap":
                                break;
                            case "mtllib":
                                break;
                            case "v":
                                mesh.vertices[v] = new Vector3(System.Convert.ToSingle(brokenString[1]), System.Convert.ToSingle(brokenString[2]),
                                                         System.Convert.ToSingle(brokenString[3]));
                                v++;
                                break;
                            case "vt":
                                mesh.uv[vt] = new Vector2(System.Convert.ToSingle(brokenString[1]), System.Convert.ToSingle(brokenString[2]));
                                vt++;
                                break;
                            case "vt1":
                                mesh.uv[vt1] = new Vector2(System.Convert.ToSingle(brokenString[1]), System.Convert.ToSingle(brokenString[2]));
                                vt1++;
                                break;
                            case "vt2":
                                mesh.uv[vt2] = new Vector2(System.Convert.ToSingle(brokenString[1]), System.Convert.ToSingle(brokenString[2]));
                                vt2++;
                                break;
                            case "vn":
                                mesh.normals[vn] = new Vector3(System.Convert.ToSingle(brokenString[1]), System.Convert.ToSingle(brokenString[2]),
                                                        System.Convert.ToSingle(brokenString[3]));
                                vn++;
                                break;
                            case "vc":
                                break;
                            case "f":

                                int j = 1;
                                List<int> intArray = new List<int>();
                                while (j < brokenString.Length && ("" + brokenString[j]).Length > 0)
                                {
                                    Vector3 temp = new Vector3();
                                    brokenBrokenString = brokenString[j].Split(splitIdentifier2, 3);    //Separate the face into individual components (vert, uv, normal)
                                    temp.x = System.Convert.ToInt32(brokenBrokenString[0]);
                                    if (brokenBrokenString.Length > 1)                                  //Some .obj files skip UV and normal
                                    {
                                        if (brokenBrokenString[1] != "")                                    //Some .obj files skip the uv and not the normal
                                        {
                                            temp.y = System.Convert.ToInt32(brokenBrokenString[1]);
                                        }
                                        temp.z = System.Convert.ToInt32(brokenBrokenString[2]);
                                    }
                                    j++;

                                    mesh.faceData[f2] = temp;
                                    intArray.Add(f2);
                                    f2++;
                                }
                                j = 1;
                                while (j + 2 < brokenString.Length)     //Create triangles out of the face data.  There will generally be more than 1 triangle per face.
                                {
                                    mesh.triangles[f] = intArray[0];
                                    f++;
                                    mesh.triangles[f] = intArray[j];
                                    f++;
                                    mesh.triangles[f] = intArray[j + 1];
                                    f++;

                                    j++;
                                }
                                break;
                        }
                        currentText = reader.ReadLine();
                        if (currentText != null)
                        {
                            currentText = currentText.Replace("  ", " ");       //Some .obj files insert double spaces, this removes them.
                        }
                    }
                }
            }
        }
    }
}
