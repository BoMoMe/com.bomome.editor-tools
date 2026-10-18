using System;
using System.Collections.Generic;

namespace Bomome.EditorTools.Build
{
    public enum BuildEnvironment { Development, Stage, Production }

    /// <summary>Consumer-owned temporary build state; never discovers game configs or SDKs.</summary>
    public static class AndroidBuildHooks
    {
        private static readonly SortedDictionary<string, Func<BuildEnvironment, IDisposable>> adapters =
            new SortedDictionary<string, Func<BuildEnvironment, IDisposable>>(StringComparer.Ordinal);

        public static IDisposable Register(string id, Func<BuildEnvironment, IDisposable> prepare)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("An adapter ID is required.", nameof(id));
            if (prepare == null) throw new ArgumentNullException(nameof(prepare));
            adapters.Add(id, prepare);
            return new Registration(id);
        }

        public static IDisposable Begin(BuildEnvironment environment)
        {
            var scope = new Scope();
            try
            {
                foreach (var adapter in adapters.Values)
                {
                    IDisposable state = adapter(environment);
                    if (state != null) scope.States.Add(state);
                }
                return scope;
            }
            catch (Exception preparationError)
            {
                try { scope.Dispose(); }
                catch (Exception cleanupError) { throw new AggregateException(preparationError, cleanupError); }
                throw;
            }
        }

        private sealed class Registration : IDisposable
        {
            private string id;
            public Registration(string id) => this.id = id;
            public void Dispose()
            {
                if (id == null) return;
                adapters.Remove(id);
                id = null;
            }
        }

        private sealed class Scope : IDisposable
        {
            internal readonly List<IDisposable> States = new List<IDisposable>();
            public void Dispose()
            {
                var errors = new List<Exception>();
                for (int i = States.Count - 1; i >= 0; i--)
                {
                    try { States[i].Dispose(); }
                    catch (Exception error) { errors.Add(error); }
                }
                States.Clear();
                if (errors.Count > 0) throw new AggregateException("Build adapter cleanup failed.", errors);
            }
        }
    }
}
