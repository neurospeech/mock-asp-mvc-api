using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;

namespace NeuroSpeech.MockMvcApi
{
    public abstract class BaseApiTest<T> :
            BaseTest
            where T : ApiController
    {
        public BaseApiTest(ITestOutputHelper writer) : base(writer)
        {
        }

        public async Task<TResult> RunAsync<TResult>(Func<T, Task<TResult>> action)
        {
            using (var tc = NewRequest())
            {
                ValidateModels(tc, action.Target);
                return await action(tc);
            }
        }

        private MethodInfo validateMethod = null;

        private void ValidateModels(ApiController c, object target)
        {
            if (validateMethod == null)
            {
                var method = typeof(ApiController).GetMethods().FirstOrDefault(x => x.Name == "Validate" && x.GetParameters()?.Length == 1);
                validateMethod = method;
            }
            foreach (var property in target.GetType().GetFields())
            {
                var value = property.GetValue(target);

                validateMethod.MakeGenericMethod(value.GetType()).Invoke(c, new object[] { value });

                if (!c.ModelState.IsValid)
                {
                    throw new HttpResponseException(
                        new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest)
                        {
                            Content = new StringContent(JsonConvert.SerializeObject(c.ModelState))
                        }
                        );
                }
            }
        }

        public Task<HttpResponseException> ResponseException(Func<T, Task> f)
        {
            return RunThrowsAsync<HttpResponseException>(f);
        }

        public async Task<TException> RunThrowsAsync<TException>(Func<T, Task> f)
            where TException : Exception
        {
            try
            {
                using (var c = NewRequest())
                {
                    ValidateModels(c, f.Target);
                    await f(c);
                }
            }
            catch (TException ex)
            {
                return ex;
            }
            //Assert.False(true, $"Expecting exception of type {typeof(TException).Name}");
            throw new InvalidOperationException($"Expecting exception of type {typeof(TException).Name}");
            //return null;
        }

        private static HttpConfiguration _EmptyConfiguration = null;
        public static HttpConfiguration EmptyConfiguration =>
            _EmptyConfiguration ?? (_EmptyConfiguration = new HttpConfiguration());

        public object Assert { get; private set; }

        private T NewRequest()
        {
            var c = Activator.CreateInstance<T>();
            c.Configuration = EmptyConfiguration;
            return c;
        }
    }
}
