using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CBot
{
    class CThreadPoll
    {
        private object l = new object();
        private int running = 0;
        private Queue<CBackgroundWorker> awaiting = new Queue<CBackgroundWorker>();
        private bool go = false;

        public readonly int MAX_THREADS;
        public readonly int WAIT;

        public CThreadPoll(int max = 1000, int wait = 1000)
        {
            MAX_THREADS = max;
            WAIT = wait;
        }

        public void Add(CBackgroundWorker worker)
        {
            worker.RunWorkerCompleted += (o, e) => { 
                lock(l) running--;
                worker.Name.Log(LogType.FINISHED);
            };
            awaiting.Enqueue(worker);
        }

        public void Init()
        {
            if (!go)
            {
                go = true;
                new Thread(() =>
                {
                    while (go)
                    {
                        if (awaiting.Count > 0 && running <= MAX_THREADS)
                        {
                            lock (l) running++;
                            var worker = awaiting.Dequeue();
                            worker.Name.Log(LogType.STARTING);
                            worker.RunWorkerAsync();
                        }
                        else
                            Thread.Sleep(WAIT);
                    }
                }).Start();
            }
        }

        public void Stop()
        {
            go = false;
        }
    }

    class CBackgroundWorker : BackgroundWorker
    {
        public readonly string Name;

        public CBackgroundWorker(string name) : base()
        {
            this.Name = name;
        }
    }
}
