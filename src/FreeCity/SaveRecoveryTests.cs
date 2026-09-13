using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using OpenTK.Mathematics;

namespace Probuzhdenie.FreeCity;

public static partial class SaveSystem
{
    public static bool RunRecoveryTests(out string message)
    {
        string root = Path.Combine(Path.GetTempPath(),"awakening-recovery-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string path = Path.Combine(root,"save.json");
        var previousLedger = MemoryRuntime.Current;
        int previousHero = MemoryRuntime.HeroId;
        void Check(bool valid, string name) { if (!valid) throw new InvalidOperationException(name); }
        void ExpectFailure(string name)
        {
            bool failed = false;
            try { LoadFromPath(path); }
            catch (SaveLoadException) { failed = true; }
            Check(failed,name);
        }
        try
        {
            int attempts = 0, waited = 0;
            var transient = new IOException("injected Windows replace failure",unchecked((int)0x80070497));
            RetryFileReplace(() => { if (++attempts < 3) throw transient; }, ms => waited += ms);
            Check(attempts==3 && waited==60,"transient replacement retries are bounded");
            attempts=waited=0;
            bool exhausted=false;
            try { RetryFileReplace(() => { attempts++; throw transient; }, ms => waited += ms); }
            catch(IOException e) when(ReferenceEquals(e,transient)) { exhausted=true; }
            Check(exhausted && attempts==3 && waited==60,"persistent replacement failure propagates");
            attempts=waited=0;
            bool refused=false;
            try { RetryFileReplace(() => { attempts++; throw new IOException("other failure"); }, ms => waited += ms); }
            catch(IOException) { refused=true; }
            Check(refused && attempts==1 && waited==0,"unrecognized errors are not retried");

            MemoryRuntime.Reset();
            Check(LoadFromPath(path).progress.Day==1 && !File.Exists(path),"absent files start a new game without writing");
            var progress = new HeroProgress();
            progress.Restore(2,12,8,6,4,2);
            var awareness = new AwarenessSystem();
            var player = new PlayerSaveData { X=11.5f,Y=.12f,Z=8.5f,Yaw=1.2f };
            Check(SaveToPath(path,424242,progress,awareness,13,DateTime.UtcNow,null,new MemoryLedger(),player),"first write");
            string first = File.ReadAllText(path);
            Check(File.ReadAllText(path+".bak")==first,"first save has a usable backup");
            progress.NewDay();
            Check(SaveToPath(path,424242,progress,awareness,14,DateTime.UtcNow,null,new MemoryLedger(),player),"second write");
            Check(File.ReadAllText(path+".bak")==first,"backup holds preceding valid save");
            string corrupt = "{\"Version\":5,\"Day\":3,";
            File.WriteAllText(path,corrupt);
            var loaded = LoadFromPath(path);
            Check(loaded.progress.Day==2 && loaded.player?.X==player.X && loaded.notice.Length>0,"recover preceding progress and position with notice");
            string archive = Directory.GetFiles(root,"save.json.corrupt-*.json").Single();
            Check(File.ReadAllText(archive)==corrupt && File.ReadAllText(path)==first && File.ReadAllText(path+".bak")==first,
                "recovery preserves damaged original and good fallback");
            Check(LoadFromPath(path).notice=="","second load uses restored primary");
            File.Delete(path);
            Check(LoadFromPath(path).progress.Day==2,"missing primary recovers backup");

            if (OperatingSystem.IsWindows())
            {
                using (var locked = new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.None))
                    ExpectFailure("locked primary must not be mistaken for missing/corrupt data");
                Check(File.ReadAllText(path)==first,"locked load did not replace primary");
                using (var locked = new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
                    Check(!SaveToPath(path,424242,progress,awareness,14,DateTime.UtcNow,null),"denied primary replacement reports failure");
                Check(File.ReadAllText(path)==first && File.ReadAllText(path+".bak")==first,"failed primary replacement keeps a valid fallback");
            }
            File.WriteAllBytes(path,new byte[] { 0xff, 0xc0 });
            Check(LoadFromPath(path).notice.Length>0,"invalid text encoding recovers");
            using (var oversized = new FileStream(path,FileMode.Create,FileAccess.Write)) oversized.SetLength(MaxSaveBytes+1);
            Check(LoadFromPath(path).notice.Length>0,"oversized input recovers");
            progress.LoadDialogueRewards(new[] { new string('x',MaxSaveBytes) });
            Check(!SaveToPath(path,424242,progress,awareness,14,DateTime.UtcNow,null),"oversized outgoing save is refused");
            Check(File.ReadAllText(path)==first && File.ReadAllText(path+".bak")==first,"size limit cannot publish an unreadable save");
            progress.LoadDialogueRewards(Array.Empty<string>());

            foreach (var invalid in new (string key, JsonNode? value)[] {
                ("Memory",JsonValue.Create(-1)), ("Awareness",JsonValue.Create(101)),
                ("Day",JsonValue.Create(0)), ("TimeOfDay",JsonValue.Create(25)),
                ("Version",JsonValue.Create("bad")), ("MemoryEvents",null),
                ("Player",JsonNode.Parse("{\"X\":1e30,\"Yaw\":0}")) })
            {
                var document = JsonNode.Parse(first)!;
                document[invalid.key] = invalid.value;
                File.WriteAllText(path,document.ToJsonString());
                Check(LoadFromPath(path).notice.Length>0,"invalid field recovers: "+invalid.key);
            }
            foreach (string malformed in new[] { "null", "[]", "{}", "{\"Seed\":1,\"seed\":2,\"Day\":1}",
                "{\"Seed\":1,\"Day\":1,\"Memory\":1e999}" })
            {
                File.WriteAllText(path,malformed);
                Check(LoadFromPath(path).progress.Day==2,"malformed save recovers");
            }
            string future = "{\"Version\":"+(CurrentSaveVersion+1)+",\"Player\":\"new schema\"}";
            File.WriteAllText(path,future);
            ExpectFailure("future primary blocks fallback even when its payload changed");
            Check(File.ReadAllText(path)==future && File.ReadAllText(path+".bak")==first,"future save untouched");
            File.WriteAllText(path,first);
            File.WriteAllText(path+".bak",future);
            Check(!SaveToPath(path,424242,progress,awareness,8,DateTime.UtcNow,null),"write protects future backup too");
            Check(File.ReadAllText(path)==first && File.ReadAllText(path+".bak")==future,"future backup not overwritten");
            File.WriteAllText(path,corrupt);
            File.WriteAllText(path+".bak",future);
            ExpectFailure("future backup is protected");
            File.WriteAllText(path+".bak","broken backup");
            ExpectFailure("two invalid files do not reset progress");
            Check(!SaveToPath(path,424242,progress,awareness,8,DateTime.UtcNow,null),"write refuses corrupt primary");
            Check(File.ReadAllText(path)==corrupt && File.ReadAllText(path+".bak")=="broken backup","failed load/write leave both files intact");

            File.WriteAllText(path,first);
            File.Delete(path+".bak");
            Directory.CreateDirectory(path+".bak");
            Check(!SaveToPath(path,424242,progress,awareness,8,DateTime.UtcNow,null),"backup write failure propagates");
            Check(File.ReadAllText(path)==first,"failed backup cannot replace primary");
            Directory.Delete(path+".bak");
            File.WriteAllText(path+".bak",first);
            player.Yaw=float.NaN;
            Check(!SaveToPath(path,424242,progress,awareness,8,DateTime.UtcNow,null,player:player),"invalid outgoing position rejected");
            Check(File.ReadAllText(path)==first && File.ReadAllText(path+".bak")==first,"invalid write preserves both saves");

            loaded = LoadFromPath(path);
            var city = new CityRenderer(loaded.seed,loaded.progress);
            city.RestorePlayer(loaded.player);
            Check(Vector3.Distance(city.Player!.Position,new Vector3(11.5f,.12f,8.5f))<.01f &&
                Math.Abs(city.Player.Rotation-1.2f)<.001f && city.Player.Velocity==Vector3.Zero,"outdoor pose and stopped motion restored");
            city = new CityRenderer(424242);
            city.RestorePlayer(new PlayerSaveData { X=4,Y=30,Z=17 });
            Check(Vector2.Distance(city.Player!.Position.Xz,new Vector2(4,17))>1 && city.Player.Position.Y<=.12f,"unsafe tram overlap and height corrected");

            var block = city.Blocks.First(b=>b.Type==BuildingType.Cafe);
            var inside = new PlayerSaveData { X=block.X+5,Y=0,Z=block.Z+5,Yaw=-1,
                InteriorCellX=block.X/CityGenerator.CellSize,InteriorCellZ=block.Z/CityGenerator.CellSize };
            Check(SaveToPath(path,424242,progress,awareness,8,DateTime.UtcNow,null,player:inside),"save interior");
            loaded = LoadFromPath(path);
            city = new CityRenderer(loaded.seed,loaded.progress);
            city.RestorePlayer(loaded.player);
            Check(city.IsInside && city.InsideBlock?.X==block.X && city.InsideBlock?.Z==block.Z &&
                city.Player!.Position==inside.Position,"interior identity and location round trip without GL");
            inside.InteriorCellX=int.MaxValue;
            Check(!SaveToPath(path,424242,progress,awareness,8,DateTime.UtcNow,null,player:inside),"invalid building id refused");
            Check(Directory.GetFiles(root,"*.tmp").Length==0,"failed writes clean temporary files");
            message = "Save recovery and player-location tests passed.";
            return true;
        }
        catch (Exception e) { message="Save recovery tests failed: "+e; return false; }
        finally
        {
            MemoryRuntime.Replace(previousLedger);
            MemoryRuntime.HeroId=previousHero;
            Directory.Delete(root,recursive:true);
        }
    }
}
