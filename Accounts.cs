using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;

namespace Nash
{
    [DataContract]
    public sealed class UserRec
    {
        [DataMember(Name = "user")] public string User;
        [DataMember(Name = "salt")] public string Salt;
        [DataMember(Name = "hash")] public string Hash;
    }

    public static class Accounts
    {
        static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GamerEdition");
        static readonly string UsersFile = Path.Combine(Dir, "users.json");
        static readonly string RememberFile = Path.Combine(Dir, "remember.bin");
        static readonly object Gate = new object();

        public static void Ensure()
        {
            Directory.CreateDirectory(Dir);
        }

        public static string Register(string name, string password)
        {
            name = (name ?? "").Trim();
            if (!IsName(name)) return "Use 3 a 20 letras ou números, sem espaço.";
            if (password == null || password.Length < 6) return "A senha precisa de pelo menos 6 caracteres.";
            lock (Gate)
            {
                var users = Load();
                var key = name.ToLowerInvariant();
                if (users.Any(u => u.User == key)) return "Esse nome já existe neste PC. Entre com ele.";
                var salt = NewHex(16);
                users.Add(new UserRec { User = key, Salt = salt, Hash = Hash(password, salt) });
                Save(users);
                return null;
            }
        }

        public static string Login(string name, string password)
        {
            name = (name ?? "").Trim().ToLowerInvariant();
            lock (Gate)
            {
                var found = Load().FirstOrDefault(u => u.User == name);
                if (found == null) return "Nome ou senha incorretos.";
                if (!string.Equals(Hash(password ?? "", found.Salt), found.Hash, StringComparison.OrdinalIgnoreCase))
                    return "Nome ou senha incorretos.";
                return null;
            }
        }

        public static string ChangePassword(string user, string current, string next)
        {
            if (next == null || next.Length < 6) return "A nova senha precisa de pelo menos 6 caracteres.";
            lock (Gate)
            {
                var users = Load();
                var found = users.FirstOrDefault(u => u.User == user);
                if (found == null || !string.Equals(Hash(current ?? "", found.Salt), found.Hash, StringComparison.OrdinalIgnoreCase))
                    return "Senha atual incorreta.";
                found.Salt = NewHex(16);
                found.Hash = Hash(next, found.Salt);
                Save(users);
                return null;
            }
        }

        public static void Remember(string user, string password)
        {
            Ensure();
            var raw = Encoding.UTF8.GetBytes((user ?? "") + "\n" + (password ?? ""));
            var protectedBytes = ProtectedData.Protect(raw, Encoding.UTF8.GetBytes("NashBoost"), DataProtectionScope.CurrentUser);
            File.WriteAllBytes(RememberFile, protectedBytes);
        }

        public static void Forget()
        {
            try { if (File.Exists(RememberFile)) File.Delete(RememberFile); } catch { }
        }

        public static bool TryRecall(out string user, out string password)
        {
            user = "";
            password = "";
            try
            {
                if (!File.Exists(RememberFile)) return false;
                var raw = ProtectedData.Unprotect(File.ReadAllBytes(RememberFile), Encoding.UTF8.GetBytes("NashBoost"), DataProtectionScope.CurrentUser);
                var text = Encoding.UTF8.GetString(raw);
                var cut = text.IndexOf('\n');
                if (cut < 0) return false;
                user = text.Substring(0, cut);
                password = text.Substring(cut + 1);
                return user.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        static bool IsName(string name)
        {
            if (name.Length < 3 || name.Length > 20) return false;
            foreach (var c in name)
            {
                if (!char.IsLetterOrDigit(c)) return false;
            }
            return true;
        }

        static List<UserRec> Load()
        {
            Ensure();
            if (!File.Exists(UsersFile)) return new List<UserRec>();
            var raw = File.ReadAllText(UsersFile);
            if (string.IsNullOrWhiteSpace(raw)) return new List<UserRec>();
            using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(raw)))
            {
                var ser = new DataContractJsonSerializer(typeof(List<UserRec>));
                var list = ser.ReadObject(ms) as List<UserRec>;
                if (list == null) return new List<UserRec>();
                return list.Where(u => u != null && !string.IsNullOrEmpty(u.User) && !string.IsNullOrEmpty(u.Salt) && !string.IsNullOrEmpty(u.Hash)).ToList();
            }
        }

        static void Save(List<UserRec> users)
        {
            Ensure();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var clean = new List<UserRec>();
            foreach (var u in users)
            {
                if (u == null || string.IsNullOrEmpty(u.User) || !seen.Add(u.User)) continue;
                clean.Add(u);
            }
            using (var ms = new MemoryStream())
            {
                var ser = new DataContractJsonSerializer(typeof(List<UserRec>));
                ser.WriteObject(ms, clean);
                File.WriteAllBytes(UsersFile, ms.ToArray());
            }
        }

        static string Hash(string password, string saltHex)
        {
            var salt = new byte[saltHex.Length / 2];
            for (var i = 0; i < salt.Length; i++) salt[i] = Convert.ToByte(saltHex.Substring(i * 2, 2), 16);
            using (var pbk = new Rfc2898DeriveBytes(password, salt, 100000))
            {
                return BitConverter.ToString(pbk.GetBytes(32)).Replace("-", "");
            }
        }

        static string NewHex(int count)
        {
            var bytes = new byte[count];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            return BitConverter.ToString(bytes).Replace("-", "");
        }
    }
}
