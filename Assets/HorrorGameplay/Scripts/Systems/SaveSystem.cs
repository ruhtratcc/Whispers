// Valida, grava e recupera o checkpoint e o estado dos objetos.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HorrorGame
{
    [Serializable]
    public sealed class SavedObject
    {
        public string id, json;
    }

    [Serializable]
    public sealed class SaveData
    {
        public int version = 2;
        public string scene;
        public Vector3 position;
        public float heading, pitch, stamina, battery, tension;
        public bool flashlightOn, crouched;
        public List<InventoryItem> inventory = new List<InventoryItem>();
        public List<SavedObject> objects = new List<SavedObject>();
        public NarrativeProgress narrative;
        public GameClockState clock;
        public PhoneMessageState phone;
        public WhistleState whistle;
        public EquipmentState equipment;
        public LockpickState lockpick;
        public TutorialCardState tutorials;
    }

    public sealed class SaveSystem : MonoBehaviour
    {
        [SerializeField]
        public FirstPersonMotor player;
        [SerializeField]
        public Inventory inventory;
        [SerializeField]
        public FlashlightController flashlight;
        [SerializeField]
        public HorrorAtmosphere atmosphere;
        [SerializeField]
        public NarrativeDirector narrative;
        [SerializeField]
        public string fileName = "save.json";
        StoryBook Book => narrative ? narrative.book : null;
        string StoryId => Book ? Book.storyId ?? "" : "";
        public string SavePath => PathForFile(BeforeDemolitionCampaign.FileName(Book, fileName));
#if UNITY_EDITOR
        static string testSaveDirectory;
        public static string TestSaveDirectory
        {
            get => testSaveDirectory;
            set
            {
                if (!string.IsNullOrEmpty(value) && !Path.IsPathFullyQualified(value))
                    throw new ArgumentException("The test save directory requires an explicit absolute path.", nameof(value));
                testSaveDirectory = string.IsNullOrEmpty(value) ? null : Path.GetFullPath(value);
            }
        }
#endif
        public static string PathForFile(string name)
        {
#if UNITY_EDITOR
            string directory = TestSaveDirectory ?? Application.persistentDataPath;
#else
            string directory = Application.persistentDataPath;
#endif
            return Path.Combine(directory, Path.GetFileName(name));
        }
        public bool SavesSuspended => GameSession.Instance && (GameSession.Instance.IsBlackingOut || GameSession.Instance.IsRestoringCheckpoint || EndingEpilogue.Active);
        public bool HasPendingSave => pendingSave != null;
        SaveData lastCheckpoint, pendingSave;
        static SaveData chapterCarry;
        static string chapterCarryPath, chapterCarryDestination;
        static SaveData resumeSnapshot;
        static string resumePath;
        static Task queuedWrites=Task.CompletedTask;
        static readonly object diskGate=new object();
        static readonly Dictionary<string,long> committedWrites=new Dictionary<string,long>();
        static long writeOrder;
        int writeRevision;
        public static bool HasQueuedWrites=>!queuedWrites.IsCompleted;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetAsyncState(){queuedWrites=Task.CompletedTask;lock(diskGate)committedWrites.Clear();ClearResume();}
        public static void ClearResume(){resumeSnapshot=null;resumePath=null;}
        public static bool StageResume(SaveData data,string path)
        {
            if(!Valid(data)||string.IsNullOrEmpty(path))return false;
            resumeSnapshot=Clone(data);resumePath=path;return true;
        }
        public bool StageCurrentResume()
        {
            if(Valid(lastCheckpoint)&&MatchesBook(lastCheckpoint))return StageResume(lastCheckpoint,SavePath);
            return TryReadCurrent(out var data)&&StageResume(data,SavePath);
        }

        public bool Save(Transform checkpoint = null)
        {
            return !SavesSuspended && Capture(out var data, checkpoint) && Write(data);
        }

        bool Capture(out SaveData data, Transform checkpoint = null)
        {
            using(LoadingMetrics.Measure("Save.Capture")){
            data = null;
            if (!player || player.IsHidden)
                return false;
            data = new SaveData
            {
                scene = SceneManager.GetActiveScene().name,
                position = checkpoint ? checkpoint.position : player.transform.position,
                heading = checkpoint ? checkpoint.eulerAngles.y : player.transform.eulerAngles.y,
                pitch = checkpoint ? 0 : player.Pitch,
                stamina = player.Stamina,
                crouched = player.IsCrouched,
                battery = flashlight ? flashlight.BatterySeconds : 0,
                flashlightOn = flashlight && ((GameSession.Instance&&GameSession.Instance.State==GameState.Tutorial||flashlight.equipment&&flashlight.equipment.PresentationLight)?flashlight.RequestedOn:flashlight.IsOn),
                tension = atmosphere ? atmosphere.Tension : 0,
                clock = GameClock.Ensure(GameSession.Instance)?.Capture(),
                phone = PhoneMessages.For(GameSession.Instance)?.Capture(),
                whistle = WhistleSystem.For(GameSession.Instance)?.Capture(),
                equipment = GameSession.Instance && GameSession.Instance.equipment ? GameSession.Instance.equipment.Capture() : null,
                lockpick = LockpickSystem.For(GameSession.Instance)?.Capture(),
                tutorials = TutorialCardSystem.For(GameSession.Instance)?.Capture(),
                inventory = Inventory.CopyItems(inventory ? inventory.items : null),
                narrative = narrative ? narrative.CaptureProgress() : null
            };
            var ids = new HashSet<string>();
            foreach (var p in FindObjectsByType<PersistentObject>(FindObjectsInactive.Include))
            {
                if (string.IsNullOrEmpty(p.persistentId) || !ids.Add(p.persistentId))
                {
                    Debug.LogError("Missing / duplicate persistent ID: " + p.name, p);
                    return false;
                }

                data.objects.Add(new SavedObject { id = p.persistentId, json = p.CaptureState() });
            }

            return Valid(data) && MatchesBook(data);
        
            }
        }

        /// <summary>Grava um estado já validado usando a mesma rotina dos checkpoints.</summary>
        public bool SaveSnapshot(SaveData data)
        {
            if (!Valid(data) || !MatchesBook(data) || data.scene != SceneManager.GetActiveScene().name) return false;
            return Write(data);
        }

        bool Write(SaveData data)
        {
            using(LoadingMetrics.Measure("Save.Write")){
                if(SavesSuspended||!Valid(data)||!MatchesBook(data))return false;
                // Grava em ordem para impedir que um checkpoint antigo sobrescreva o mais recente.
                int revision=++writeRevision;
                return FinishWrite(data,WriteDisk(SavePath,JsonUtility.ToJson(data,true),++writeOrder),revision);
            }
        }
        public Task<bool> SaveAsync()
        {
            if(SavesSuspended||!Capture(out var data))return Task.FromResult(false);
            return QueueWrite(data);
        }
        Task<bool> QueueWrite(SaveData data)
        {
            var snapshot=Clone(data);var payload=JsonUtility.ToJson(snapshot,true);var path=SavePath;
            int revision=++writeRevision;lastCheckpoint=Clone(snapshot);
            var previous=queuedWrites;long order=++writeOrder;
            var operation=WriteQueued(previous,snapshot,payload,path,revision,order);
            queuedWrites=operation;return operation;
        }
        async Task<bool> WriteQueued(Task previous,SaveData data,string payload,string path,int revision,long order)
        {
            try{await previous;}catch(Exception earlier){Debug.LogWarning("Previous checkpoint task failed: "+earlier.Message);}
            double milliseconds=0;
            var error=await Task.Run(()=>{double started=LoadingMetrics.Now;var result=WriteDisk(path,payload,order);milliseconds=(LoadingMetrics.Now-started)*1000;return result;});
            LoadingMetrics.DiskWrite(data.scene,milliseconds,error==null);
            if(!this){if(error!=null)Debug.LogWarning("Checkpoint could not be saved: "+error.Message);return error==null;}
            return FinishWrite(data,error,revision);
        }
        static Exception WriteDisk(string path,string payload,long order)
        {
            lock(diskGate){
            try{
                if(committedWrites.TryGetValue(path,out long latest)&&latest>order)return null;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path+".tmp",payload);
                if(File.Exists(path))File.Replace(path+".tmp",path,path+".bak");
                else File.Move(path+".tmp",path);
                committedWrites[path]=order;return null;
            }catch(Exception error){return error;}
            }
        }
        bool FinishWrite(SaveData data,Exception error,int revision)
        {
            if(error==null&&revision==writeRevision)
                try{PlayerPrefs.SetString(BeforeDemolitionCampaign.Key(Book,"SaveScene"),data.scene);PlayerPrefs.Save();lastCheckpoint=Clone(data);pendingSave=null;}
                catch(Exception metadataError){error=metadataError;}
            if(error!=null){if(revision==writeRevision)pendingSave=Clone(data);Debug.LogWarning("Checkpoint could not be saved: "+error.Message,this);return false;}
            return true;
        }

        public bool RetryPendingSave() => pendingSave != null && Write(pendingSave);

        public bool StageChapterCarry(string destination)
        {
            if (SavesSuspended || !Capture(out var data)) return false;
            chapterCarry = Clone(data);
            chapterCarryPath = SavePath;
            chapterCarryDestination = destination;
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ClearChapterCarry()
        {
            chapterCarry = null;
            chapterCarryPath = chapterCarryDestination = null;
        }

        public bool LoadCurrent()
        {
            var staged=resumePath==SavePath&&resumeSnapshot!=null&&resumeSnapshot.scene==SceneManager.GetActiveScene().name&&MatchesBook(resumeSnapshot)?resumeSnapshot:null;
            ClearResume();
            return staged!=null?RestoreSnapshot(staged):TryReadCurrent(out var data)&&RestoreSnapshot(data);
        }

        public bool TryReadCurrent(out SaveData data)
        {
            string scene = SceneManager.GetActiveScene().name;
            if (TryReadCheckpoint(SavePath, out data, scene, StoryId) && MatchesBook(data)) return true;
            data = Valid(lastCheckpoint) && MatchesBook(lastCheckpoint) && lastCheckpoint.scene == scene ? Clone(lastCheckpoint) : null;
            return data != null;
        }

        public static bool TryReadCheckpoint(string path, out SaveData data, string expectedScene = null, string expectedStoryId = null)
        {
            foreach (string candidatePath in new[] { path, path + ".bak" })
                if (TryRead(candidatePath, out var candidate)
                    && (expectedStoryId == null || MatchesStory(candidate, expectedStoryId))
                    && (expectedScene != null ? candidate.scene == expectedScene : Application.CanStreamedLevelBeLoaded(candidate.scene)))
                { data = candidate; return true; }
            data = null;
            return false;
        }

        /// <summary>Restaura o jogo durante o apagão. Quando falta um registro antigo, usa o estado da cena.</summary>
        public bool RestoreSnapshot(SaveData data, List<SavedObject> missingStates = null)
        {
            using(LoadingMetrics.Measure("Save.RestoreSnapshot")){
            if (!player || !Valid(data) || !MatchesBook(data) || data.scene != SceneManager.GetActiveScene().name) return false;
            GameClock.Ensure(GameSession.Instance)?.Restore(data.clock);
            PhoneMessages.For(GameSession.Instance)?.Restore(data.phone);
            if (narrative) narrative.RestoreProgress(data.narrative);
            if (inventory) inventory.Restore(data.inventory);
            WhistleSystem.For(GameSession.Instance)?.Restore(data.whistle);
            foreach (var p in FindObjectsByType<PersistentObject>(FindObjectsInactive.Include))
            {
                var saved = data.objects?.Find(x => x != null && x.id == p.persistentId)
                    ?? missingStates?.Find(x => x != null && x.id == p.persistentId);
                if (saved != null)
                    try
                    {
                        p.RestoreState(saved.json);
                    }
                    catch (Exception error)
                    {
                        Debug.LogWarning("Object state skipped: " + error.Message, p);
                    }
            }

            player.Warp(data.position, data.heading, data.pitch, false);
            player.RestoreStance(data.crouched || !player.CanStand());
            player.RestoreStamina(data.stamina);
            flashlight?.Restore(data.battery, data.flashlightOn);
            if(GameSession.Instance&&GameSession.Instance.equipment)GameSession.Instance.equipment.Restore(data.equipment,data.flashlightOn);
            LockpickSystem.For(GameSession.Instance)?.Restore(data.lockpick);
            TutorialCardSystem.For(GameSession.Instance)?.Restore(data.tutorials);
            atmosphere?.SetTension(data.tension);
            if (SavesSuspended)
                foreach (var checkpoint in FindObjectsByType<Checkpoint>(FindObjectsInactive.Include))
                    checkpoint.ResetAfterRestore();
            lastCheckpoint = Clone(data);
            pendingSave = null;
            return true;
        
            }
        }

        static SaveData Clone(SaveData data) => JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data));

        public bool LoadChapterCarry()
        {
            using(LoadingMetrics.Measure("Save.LoadCarry")){
            if (!narrative || !narrative.book) return false;
            var carried = chapterCarryPath == SavePath && chapterCarryDestination == SceneManager.GetActiveScene().name ? chapterCarry : null;
            ClearChapterCarry();
            if (Valid(carried) && MatchesBook(carried) && RestoreChapterCarry(carried)) return true;
            foreach (string path in new[] { SavePath, SavePath + ".bak" })
                if (TryRead(path, out var data) && MatchesBook(data) && RestoreChapterCarry(data)) return true;
            return false;
        
            }
        }

        bool RestoreChapterCarry(SaveData data)
        {
            if (!Valid(data) || !MatchesBook(data)) return false;
            var progress = data.narrative;
            // O último capítulo continua no escritório, dentro do mesmo capítulo.
            bool officeReturn = data.scene == "SchoolCEEP" && SceneManager.GetActiveScene().name == "Chapter1Office"
                && progress != null && progress.storyRevision == 2 && progress.chapterId == "last-exit"
                && progress.HasSignal("last-exit.outside") && !progress.endingSeen;
            var destination = progress != null ? narrative.book.Chapter(progress.pendingChapterId) : null;
            if (!officeReturn && (destination == null || destination.sceneName != SceneManager.GetActiveScene().name
                || !progress.IsChapterComplete(narrative.book))) return false;
            progress = progress.Copy();
            if (!officeReturn && !progress.EnterPendingChapter(narrative.book)) return false;
            GameClock.Ensure(GameSession.Instance)?.Restore(data.clock);
            GameClock.Instance?.EnterScene();
            PhoneMessages.For(GameSession.Instance)?.Restore(data.phone);
            narrative.RestoreProgress(progress);
            if (inventory) inventory.Restore(data.inventory);
            WhistleSystem.For(GameSession.Instance)?.Restore(data.whistle);
            // Usa o ponto de partida do novo mapa. As posições do mapa anterior ficam no mapa anterior.
            if (flashlight) flashlight.Restore(data.battery, data.flashlightOn);
            if(GameSession.Instance&&GameSession.Instance.equipment)GameSession.Instance.equipment.Restore(data.equipment,data.flashlightOn);
            LockpickSystem.For(GameSession.Instance)?.Restore(data.lockpick);
            TutorialCardSystem.For(GameSession.Instance)?.Restore(data.tutorials);
            if (atmosphere) atmosphere.SetTension(data.tension);
            // Este checkpoint usa o ponto de chegada e os objetos do novo mapa, mesmo sem acesso ao disco.
            if (Capture(out var arrival)) lastCheckpoint = Clone(arrival);
            return true;
        }

        public static bool TryRead(string path, out SaveData data)
        {
            using(LoadingMetrics.Measure("Save.Read")){
            data = null;
            try
            {
                if (!File.Exists(path))
                    return false;
                var candidate = new SaveData { version = 0, narrative = null };
                JsonUtility.FromJsonOverwrite(File.ReadAllText(path), candidate);
                if (!Valid(candidate))
                    return false;
                data = candidate;
                return true;
            }
            catch
            {
                return false;
            }
        
            }
        }

        static bool Valid(SaveData data)
        {
            if (data != null && data.clock != null && !data.clock.Valid) return false;
            if (data != null && data.phone != null && !data.phone.Valid) return false;
            if (data != null && data.whistle != null && !data.whistle.Valid) return false;
            if (data != null && data.equipment != null && !data.equipment.Valid) return false;
            if (data != null && data.lockpick != null && !data.lockpick.Valid) return false;
            if (data != null && data.tutorials != null && !data.tutorials.Valid) return false;
            if (data == null || (data.version != 1 && data.version != 2) || string.IsNullOrWhiteSpace(data.scene)
                || !Finite(data.position.x) || !Finite(data.position.y) || !Finite(data.position.z)
                || !Finite(data.heading) || !Finite(data.pitch) || !Finite(data.battery)
                || !Finite(data.stamina) || !Finite(data.tension)) return false;
            var items = new HashSet<string>(StringComparer.Ordinal);
            if (data.inventory != null)
                foreach (var item in data.inventory)
                    if (item == null || string.IsNullOrWhiteSpace(item.id) || !items.Add(item.id)) return false;
            var progress = data.narrative;
            if (progress != null && progress.storyId == BeforeDemolitionCampaign.StoryId)
                return BeforeDemolitionCampaign.ValidCheckpoint(data, items);
            if (progress != null && !string.IsNullOrEmpty(progress.storyId)) return false;
            // Atualiza os dados das histórias antigas antes de verificar os requisitos dos capítulos atuais.
            if (progress == null || progress.storyRevision == 0 || progress.storyRevision == 1) return true;
            if (progress.storyRevision != 2) return false;
            switch (progress.chapterId)
            {
                case "prologue": return data.scene == "Chapter1Office";
                case "road": return data.scene == "RoadJourney";
                case "school": return data.scene == "SchoolCEEP";
                case "left-behind": return data.scene == "SchoolCEEP" && items.Contains("iron_key");
                case "memories": return data.scene == "SchoolCEEP" && items.Contains("iron_key") && items.Contains("room_113_key");
                case "last-exit":
                    return items.Contains("iron_key") && items.Contains("room_113_key")
                        && (data.scene == "SchoolCEEP" || data.scene == "Chapter1Office"
                            && progress.milestones != null && progress.milestones.Contains("last-exit.outside"));
                default: return false;
            }
        }
        static bool MatchesStory(SaveData data, string storyId) => (data?.narrative?.storyId ?? "") == storyId;
        bool MatchesBook(SaveData data) => MatchesStory(data, StoryId)
            && (!BeforeDemolitionCampaign.Is(Book) || data.narrative.storyRevision == Book.revision
                && Book.ChapterIndex(data.narrative.chapterId) >= 0);
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

