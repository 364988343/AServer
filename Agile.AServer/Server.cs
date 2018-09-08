﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Agile.AServer.utils;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Agile.AServer
{
    public interface IServer
    {
        IServer SetPort(int port);
        IWebHost Host { get; }

        IServer AddHandler(HttpHandler handler);

        Task Run();

        Task Stop();
    }

    public class Server : IServer
    {
        private static object _lockObj = new object();
        private readonly List<HttpHandler> _handlers = new List<HttpHandler>();
        private readonly ConcurrentDictionary<string, HttpHandler> _handlersCache = new ConcurrentDictionary<string, HttpHandler>();
        private int _port = 5000;

        public IWebHost Host { get; private set; }

        public IServer SetPort(int port)
        {
            _port = 5000;

            return this;
        }

        public Task Run()
        {
            Host =
                new WebHostBuilder()
                    .UseKestrel(op => op.ListenAnyIP(_port))
                    .Configure(app =>
                    {
                        app.Run(http =>
                        {
                            var req = http.Request;
                            var resp = http.Response;
                            var method = http.Request.Method;
                            var path = req.Path;

                            var cacheKey = $"{method}-{path}";

                            _handlersCache.TryGetValue(cacheKey, out HttpHandler handler);
                            if (handler == null)
                            {
                                handler = _handlers.FirstOrDefault(h => h.Method == method && PathUtil.IsMatch(path, h.Path));
                                if (handler != null)
                                {
                                    _handlersCache.TryAdd(cacheKey, handler);
                                }
                            }

                            if (handler != null)
                            {
                                try
                                {
                                    return handler.Handler(new Request(req, handler.Path), new Response(resp));
                                }
                                catch (Exception e)
                                {
                                    Console.WriteLine(e);

                                    resp.StatusCode = (int)HttpStatusCode.InternalServerError;
                                    return resp.WriteAsync("InternalServerError");
                                }
                            }

                            resp.StatusCode = (int)HttpStatusCode.NotFound;
                            return resp.WriteAsync("NotFound");
                        });
                    })
                    .Build();
            var task = Host.StartAsync();

            Console.WriteLine("AServer listen http requests now .");

            return task;
        }

        public IServer AddHandler(HttpHandler handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }
            if (string.IsNullOrEmpty(handler.Method))
            {
                throw new ArgumentNullException("handler.Method");
            }
            if (string.IsNullOrEmpty(handler.Path))
            {
                throw new ArgumentNullException("handler.Path");
            }
            if (handler.Handler == null)
            {
                throw new ArgumentNullException("handler.Handler");
            }

            if (_handlers.Any(h => h.Path.Equals(handler.Path, StringComparison.CurrentCultureIgnoreCase) &&
                                   h.Method == handler.Method))
            {
                throw new Exception($"request path:{handler.Path} only can be set 1 handler");
            }
            else
            {
                lock (_lockObj)
                {
                    _handlers.Add(handler);
                }
            }

            return this;
        }



        public Task Stop()
        {
            return Host?.StopAsync();
        }
    }
}
