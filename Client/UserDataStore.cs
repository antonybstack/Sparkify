using System.Collections.Concurrent;

namespace Client
{
    public class UserDataStore
    {
        private readonly ConcurrentDictionary<string, User> _users = new ConcurrentDictionary<string, User>();

        public void AddOrUpdateUser(string userId, User user)
        {
            _users.AddOrUpdate(userId, user, (_, __) => user);
        }

        public IReadOnlyDictionary<string, User> Users => _users;
    }

}
