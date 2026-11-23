// Valida e guarda os IDs dos emails entregues e lidos.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace HorrorGame
{
    [Serializable]
    public sealed class EmailInboxState
    {
        public string storyId = "before-demolition";
        public int version = 1;
        public List<string> deliveredIds = new List<string>();
        public List<string> readIds = new List<string>();

        public static EmailInboxState Create(EmailCatalog catalog)
        {
            if (!catalog) throw new ArgumentNullException(nameof(catalog));
            if (catalog.ValidateContent().Length != 0) throw new ArgumentException("Catálogo de emails inválido.", nameof(catalog));
            var state = new EmailInboxState();
            foreach (var message in catalog.messages)
                if (message.initiallyDelivered) state.deliveredIds.Add(message.id);
            return state;
        }

        public bool Validate(EmailCatalog catalog)
        {
            if (!catalog || catalog.ValidateContent().Length != 0 || storyId != "before-demolition" || version != 1 ||
                deliveredIds == null || readIds == null) return false;
            var delivered = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in deliveredIds)
                if (string.IsNullOrWhiteSpace(id) || catalog.Find(id) == null || !delivered.Add(id)) return false;
            var read = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in readIds)
                if (string.IsNullOrWhiteSpace(id) || !delivered.Contains(id) || !read.Add(id)) return false;
            return true;
        }

        public bool Deliver(EmailCatalog catalog, string id)
        {
            if (!Validate(catalog) || catalog.Find(id) == null || IsDelivered(id)) return false;
            deliveredIds.Add(id);
            return true;
        }

        public bool MarkRead(EmailCatalog catalog, string id)
        {
            if (!Validate(catalog) || !IsDelivered(id) || IsRead(id)) return false;
            readIds.Add(id);
            return true;
        }

        public bool IsDelivered(string id) => id != null && deliveredIds != null && deliveredIds.Contains(id);
        public bool IsRead(string id) => id != null && readIds != null && readIds.Contains(id);

        public EmailInboxState Copy() => new EmailInboxState
        {
            storyId = storyId,
            version = version,
            deliveredIds = deliveredIds == null ? null : new List<string>(deliveredIds),
            readIds = readIds == null ? null : new List<string>(readIds)
        };
    }

    public sealed class EmailProgressStore
    {
        public string FilePath { get; }

        public EmailProgressStore(string absolutePath)
        {
            if (string.IsNullOrWhiteSpace(absolutePath) || !Path.IsPathFullyQualified(absolutePath))
                throw new ArgumentException("O progresso de emails exige um caminho absoluto explícito.", nameof(absolutePath));
            FilePath = Path.GetFullPath(absolutePath);
        }

        public bool TryLoad(EmailCatalog catalog, out EmailInboxState state, out string error)
        {
            state = null;
            error = string.Empty;
            if (!catalog || catalog.ValidateContent().Length != 0)
            {
                error = "Não foi possível carregar emails: catálogo inválido.";
                return false;
            }
            if (TryRead(FilePath, catalog, out state, out var primaryMissing, out var primaryError)) return true;
            if (TryRead(FilePath + ".bak", catalog, out state, out var backupMissing, out var backupError)) return true;
            if (primaryMissing && backupMissing)
            {
                state = EmailInboxState.Create(catalog);
                return true;
            }
            error = "Não foi possível carregar o progresso de emails. Principal: " + primaryError + " Backup: " + backupError;
            return false;
        }

        public bool TrySave(EmailInboxState state, EmailCatalog catalog, out string error)
        {
            error = string.Empty;
            if (state == null || !state.Validate(catalog))
            {
                error = "Não foi possível salvar emails: progresso inválido.";
                return false;
            }
            try
            {
                var bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(state, true));
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                var temporary = FilePath + ".tmp";
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (File.Exists(FilePath)) File.Replace(temporary, FilePath, FilePath + ".bak");
                else File.Move(temporary, FilePath);
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                error = "Leituras não salvas. Tente salvar novamente.";
                return false;
            }
        }

        private static bool TryRead(string path, EmailCatalog catalog, out EmailInboxState state,
            out bool missing, out string error)
        {
            state = null;
            missing = false;
            error = string.Empty;
            try
            {
                var json = File.ReadAllText(path, Encoding.UTF8);
                // Missing identity/list fields must remain invalid instead of receiving constructor defaults.
                var candidate = new EmailInboxState { storyId = null, version = 0, deliveredIds = null, readIds = null };
                JsonUtility.FromJsonOverwrite(json, candidate);
                if (!candidate.Validate(catalog))
                {
                    error = "conteúdo incompatível ou inválido.";
                    return false;
                }
                state = candidate;
                return true;
            }
            catch (Exception exception) when (exception is FileNotFoundException || exception is DirectoryNotFoundException)
            {
                missing = true;
                error = "arquivo ausente.";
                return false;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException)
            {
                error = "não foi possível ler o arquivo.";
                return false;
            }
        }
    }
}
