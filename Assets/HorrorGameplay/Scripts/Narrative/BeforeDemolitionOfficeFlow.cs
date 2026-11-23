// Coordena o PC, os emails, a gaveta e a saída do escritório.

using System;
using System.Linq;
using UnityEngine;

namespace HorrorGame
{
    [DisallowMultipleComponent]
    public sealed class BeforeDemolitionOfficeFlow : PersistentObject
    {
        public GameSession game;
        public NarrativeDirector director;
        public EmailInboxController inbox;
        public InteractiveDrawer secondDrawer;
        public InteractiveDoor exitDoor;
        public Transform closetDoor;
        public GameObject whistleVisual;
        public InspectionItemData whistleData;

        [Serializable] sealed class State { public EmailInboxState emails; }

        Inventory subscribedInventory;
        EmailInboxController subscribedInbox;
        NarrativeDirector subscribedDirector;
        Quaternion closedClosetRotation;
        bool closetPoseCached, reconciling, diaryDirty = true, diaryWasOpen;
        string lastObjective;

        Inventory Inventory => game && game.interaction ? game.interaction.inventory : director ? director.inventory : null;
        public bool IsActiveOffice => isActiveAndEnabled && director && BeforeDemolitionCampaign.Is(director.book)
            && director.book.revision == BeforeDemolitionCampaign.Revision
            && (director.IsInitialized ? director.Progress.chapterId : director.startingChapterId) == BeforeDemolitionCampaign.OfficeChapterId;
        public bool HasWhistle => Inventory && Inventory.Contains(BeforeDemolitionCampaign.WhistleItemId);
        public bool DrawerUnlocked => inbox && inbox.State != null && inbox.State.IsRead("email.06");
        bool RequiredEmailsRead => inbox && inbox.State != null && inbox.State.IsRead("email.01")
            && inbox.State.IsRead("email.06") && inbox.State.IsRead("email.07");
        public bool CanExit => IsActiveOffice && RequiredEmailsRead && HasWhistle;
        public bool CanCollectWhistle => IsActiveOffice && director.IsInitialized && game && game.State == GameState.Playing
            && !game.IsRestoringCheckpoint && Inventory && !HasWhistle
            && DrawerUnlocked && secondDrawer && secondDrawer.IsOpen && secondDrawer.OpenFraction >= .65f;
        public string ExitBlockedReason => HasWhistle
            ? "Preciso terminar de ler os emails 1, 6 e 7 antes de sair."
            : RequiredEmailsRead ? "Preciso abrir a segunda gaveta e pegar o que estiver dentro."
            : "Preciso ler os emails 1, 6 e 7 e pegar o apito antes de sair.";

        void Awake()
        {
            using(LoadingMetrics.Measure("Office.Awake")){
            if (!game) game = GetComponent<GameSession>();
            if (!director && game) director = game.narrative;
            if (!director) director = GetComponent<NarrativeDirector>();
            CacheClosetPose();
            if (!IsActiveOffice || !inbox) return;
            inbox.game = game;
            inbox.openOnStart = false;
            // The campaign checkpoint owns emails and world state in the same snapshot.
            inbox.Configure(inbox.catalog);
        
            }
        }

        void OnEnable()
        {
            if (!IsActiveOffice) return;
            subscribedInbox = inbox;
            subscribedInventory = Inventory;
            subscribedDirector = director;
            if (game) game.StateChanged += OfficeStateChanged;
            if (subscribedInbox) subscribedInbox.ReadFlagRaised += EmailRead;
            if (subscribedInventory) subscribedInventory.Changed += InventoryChanged;
            if (subscribedDirector) subscribedDirector.Initialized += Initialized;
            RestoreWorldSilently();
        }

        void OnDisable()
        {
            if (game) game.StateChanged -= OfficeStateChanged;
            if (subscribedInbox) subscribedInbox.ReadFlagRaised -= EmailRead;
            if (subscribedInventory) subscribedInventory.Changed -= InventoryChanged;
            if (subscribedDirector) subscribedDirector.Initialized -= Initialized;
            subscribedInbox = null;
            subscribedInventory = null;
            subscribedDirector = null;
            diaryWasOpen = false;
        }

        void CacheClosetPose()
        {
            if (!closetPoseCached && closetDoor)
            {
                closedClosetRotation = closetDoor.localRotation;
                closetPoseCached = true;
            }
        }

        void Initialized()
        {
            lastObjective = null;
            Reconcile(true);
        }

        void EmailRead(string flag)
        {
            if (!IsActiveOffice) return;
            Reconcile(true);
            director.ScheduleSave();
        }

        void OfficeStateChanged(GameState state)
        {
            if (state != GameState.Playing || !IsActiveOffice || !RequiredEmailsRead || HasWhistle) return;
            lastObjective = null;
            RefreshObjective();
        }

        void InventoryChanged()
        {
            if (!IsActiveOffice || reconciling) return;
            Reconcile(true);
            if (game && !game.IsRestoringCheckpoint) director.ScheduleSave();
        }

        public void RestoreWorldSilently() => Reconcile(false);

        void Reconcile(bool allowReadySignal)
        {
            if (!IsActiveOffice || reconciling || !inbox || inbox.State == null) return;
            reconciling = true;
            try
            {
                if(exitDoor){exitDoor.SetOpen(false,true);exitDoor.enabled=false;}
                var inventory = Inventory;
                if (inventory && inbox.catalog)
                    for (int number = 1; number <= 7; number++)
                    {
                        string id = "email.0" + number;
                        if (!inbox.State.IsRead(id)) continue;
                        var message = inbox.catalog.Find(id);
                        if (message == null) continue;
                        inventory.Add(new InventoryItem
                        {
                            id = "bd.note." + id,
                            title = "Email " + number + " — " + message.subject,
                            text = message.PlainText,
                            note = true
                        });
                    }
                if (whistleVisual) whistleVisual.SetActive(!HasWhistle);
                CacheClosetPose();
                if (GetComponent<OfficeWardrobe>()) GetComponent<OfficeWardrobe>().Reconcile();
                else if (closetDoor && closetPoseCached)
                    closetDoor.localRotation = closedClosetRotation * Quaternion.Euler(0, inbox.State.IsRead("email.07") ? -18 : 0, 0);
                if (allowReadySignal && director.IsInitialized && game && !game.IsRestoringCheckpoint && CanExit
                    && director.NextSignal == BeforeDemolitionCampaign.ReadySignal
                    && !director.HasSignal(BeforeDemolitionCampaign.ReadySignal))
                    director.Signal(BeforeDemolitionCampaign.ReadySignal);
                if(CanExit)SceneTransitionService.Preload("RoadJourney",true);
                RefreshObjective();
                diaryDirty = true;
            }
            finally { reconciling = false; }
        }

        void RefreshObjective()
        {
            string objective = !RequiredEmailsRead ? "Ler os emails 1, 6 e 7 no computador do escritório."
                : !HasWhistle ? "Pegue o que estiver dentro da segunda gaveta da mesa."
                : "Sair do escritório e continuar a investigação.";
            if (objective == lastObjective || !game || !game.hud) return;
            lastObjective = objective;
            game.hud.ShowObjective(objective);
        }

        void LateUpdate()
        {
            if (!IsActiveOffice || !game || !game.hud || !game.hud.inventoryPanel) return;
            bool open = game.hud.inventoryPanel.activeSelf;
            if (open && (!diaryWasOpen || diaryDirty) && game.hud.inventoryText && Inventory)
            {
                var items = Inventory.items;
                string notes = items.Count == 0 ? "Ainda não há anotações."
                    : string.Join("\n\n", items.Select(item => item.title + (item.note ? "\n" + item.text : "")));
                game.hud.inventoryText.text = "OBJETIVO\n" + game.hud.objective.text + "\n\nDIÁRIO E EQUIPAMENTO\n" + notes;
                diaryDirty = false;
            }
            diaryWasOpen = open;
        }

        public void OpenComputer()
        {
            if (IsActiveOffice && game && game.CanControl && inbox && !inbox.IsOpen)
                inbox.Open(EmailDevice.Desktop);
        }

        public bool ToggleDrawer(PlayerInteraction player)
        {
            if (!IsActiveOffice || !player || !game || !game.CanControl || !secondDrawer) return false;
            if (!DrawerUnlocked)
            {
                player.ShowMessage("Preciso conferir os emails no computador antes de abrir esta gaveta.");
                return false;
            }
            secondDrawer.Interact(player);
            director.ScheduleSave();
            return true;
        }

        public bool CollectWhistle(PlayerInteraction player)
        {
            // Inspection completes after control is released, before its one-frame input gate expires.
            if (!player || player.inventory != Inventory || !CanCollectWhistle) return false;
            if (!Inventory.Add(new InventoryItem
            {
                id = BeforeDemolitionCampaign.WhistleItemId,
                title = whistleData ? whistleData.title : "Apito antigo",
                text = whistleData ? whistleData.backText : "",
                note = whistleData && whistleData.recordInNotes,
                inspectionId = whistleData ? whistleData.itemId : BeforeDemolitionCampaign.WhistleItemId
            })) return false;
            if (whistleVisual) whistleVisual.SetActive(false);
            Reconcile(true);
            director.ScheduleSave();
            return true;
        }

        public bool TryExit(PlayerInteraction player)
        {
            if (!IsActiveOffice || !player || !game || !game.CanControl || !director.IsInitialized) return false;
            Reconcile(true);
            if (!CanExit)
            {
                player.ShowMessage(ExitBlockedReason);
                return false;
            }
            if(director.NextSignal == BeforeDemolitionCampaign.ExitSignal)GameClock.Instance?.SetOfficeExit();
            return director.NextSignal == BeforeDemolitionCampaign.ExitSignal
                && director.Signal(BeforeDemolitionCampaign.ExitSignal);
        }

        public void InspectCloset(PlayerInteraction player)
        {
            var wardrobe=GetComponent<OfficeWardrobe>();if(IsActiveOffice&&player&&game&&game.CanControl&&wardrobe&&wardrobe.Toggle()){director.ScheduleSave();return;}
            if (IsActiveOffice && player && game && game.CanControl)
                player.ShowMessage(inbox && inbox.State != null && inbox.State.IsRead("email.07")
                    ? "A porta do armário está entreaberta." : "A porta do armário está fechada.");
        }

        public override string CaptureState()
        {
            if (!inbox || !inbox.catalog) throw new InvalidOperationException("O escritório precisa de um catálogo de emails.");
            if (inbox.State == null) inbox.Configure(inbox.catalog);
            return JsonUtility.ToJson(new State { emails = inbox.CaptureState() });
        }

        public override void RestoreState(string json)
        {
            if (!inbox || !inbox.catalog) throw new InvalidOperationException("O escritório precisa de um catálogo de emails.");
            var restored = new State { emails = new EmailInboxState
                { storyId = null, version = 0, deliveredIds = null, readIds = null } };
            JsonUtility.FromJsonOverwrite(json, restored);
            if (restored.emails == null || !restored.emails.Validate(inbox.catalog) || !inbox.RestoreState(restored.emails))
                throw new ArgumentException("O checkpoint de emails do escritório é inválido.", nameof(json));
            RestoreWorldSilently();
        }
    }
}
