using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Linq.Expressions;
using System.Threading;

namespace RetryIt
{
    public class Retry
    {
        /// <summary>
        /// 重试次数，默认为3次
        /// </summary>
        private int times = 3;

        /// <summary>
        /// 重试间隔，默认为2s
        /// </summary>
        private TimeSpan interval = TimeSpan.FromSeconds(2);

        /// <summary>
        /// 异常及其条件集合
        /// </summary>
        private Dictionary<Type, Predicate<Exception>> cachers 
            = new Dictionary<Type, Predicate<Exception>>();

        /// <summary>
        /// 返回值条件集合
        /// </summary>
        private IList<Predicate<object>> returns = 
            new List<Predicate<object>>();

        /// <summary>
        /// 异常处理回调
        /// </summary>
        private Action<int, Exception> reject;

        /// <summary>
        /// 默认实例
        /// </summary>
        public static Retry Default => new Retry();

        /// <summary>
        /// 构造函数，默认私有
        /// </summary>
        private Retry()
        {
            this.cachers.Add(typeof(Exception), null);
            this.cachers.Add(typeof(AggregateException), null);
        }

        /// <summary>
        /// 设置重试次数
        /// </summary>
        /// <param name="times">重试次数</param>
        /// <returns></returns>
        public Retry Times(int times)
        {
            this.times = times;
            return this;
        }

        /// <summary>
        /// 设置重试间隔
        /// </summary>
        /// <param name="seconds">重试间隔，单位为：秒</param>
        /// <returns></returns>
        public Retry Interval(int seconds)
        {
            this.interval = TimeSpan.FromSeconds(seconds);
            return this;
        }

        /// <summary>
        /// 拦截指定异常
        /// </summary>
        /// <typeparam name="TException">异常类型</typeparam>
        /// <param name="expression">异常表达式</param>
        /// <returns></returns>
        public Retry Catch<TException>(Expression<Predicate<TException>> expression = null) where TException : Exception
        {
            var condition = expression == null ? null : (expression.Compile() as Predicate<Exception>);
            if (this.cachers.ContainsKey(typeof(TException)))
            {
                this.cachers[typeof(TException)] = condition;
            }
            else
            {
                this.cachers.Add(typeof(TException), condition);
            }

            return this;
        }

        /// <summary>
        /// 拦截指定结果
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="expression"></param>
        /// <returns></returns>
        public Retry Result<TResult>(Expression<Predicate<TResult>> expression = null)
        {
            //var condition = expression == null ? null : (expression.Compile() as Predicate<object>);
            //if (this.returns.Contains(typeof(TResult)))
            //{
            //    this.cachers[typeof(TException)] = condition;
            //}
            //else
            //{
            //    this.cachers.Add(typeof(TException), condition);
            //}

            return this;
        }

        /// <summary>
        /// 设置异常处理回调
        /// </summary>
        /// <param name="action">action</param>
        /// <returns></returns>
        public Retry Reject(Action<int,Exception> action)
        {
            this.reject = action;
            return this;
        }

        /// <summary>
        /// 执行方法
        /// </summary>
        /// <param name="action">action}</param>
        public void Execute(Action action)
        {
            var count = 0;
            var exceptions = new List<Exception>();
            while (true)
            {
                try
                {
                    action();
                    break;
                } 
                catch (Exception ex)
                {
                    count++;
                    times--;
                    if (reject != null) reject(count, ex);
                    if (times <= 0) throw new AggregateException(exceptions);
                    if (cachers.Keys.Contains(ex.GetType())) exceptions.Add(ex);
                    Thread.Sleep(interval);
                }
            }
        }

        /// <summary>
        /// 执行方法(异步)
        /// </summary>
        /// <param name="action">action</param>
        /// <returns></returns>
        public async Task ExecuteAsync(Action action)
        {
            var count = 0;
            var exceptions = new List<Exception>();
            while (true)
            {
                try
                {
                    await Task.Run(action);
                }
                catch (Exception ex)
                {
                    count++;
                    times--;
                    if (reject != null) reject(count, ex);
                    if (times <= 0) throw new AggregateException(exceptions);
                    if (cachers.Keys.Contains(ex.GetType())) exceptions.Add(ex);
                    Thread.Sleep(interval);
                }
            }
        }

        /// <summary>
        /// 执行方法
        /// </summary>
        /// <typeparam name="TResult">返回值类型</typeparam>
        /// <param name="func">func</param>
        /// <returns></returns>
        public TResult Execute<TResult>(Func<TResult> func)
        {
            var count = 0;
            var exceptions = new List<Exception>();
            while (true)
            {
                try
                {
                    var result = func();
                    return result;
                }
                catch (Exception ex)
                {
                    count++;
                    times--;
                    if (reject != null) reject(count, ex);
                    if (times <= 0) throw new AggregateException(exceptions);
                    if (cachers.Keys.Contains(ex.GetType())) exceptions.Add(ex);
                    Thread.Sleep(interval);
                }
            }
        }

        /// <summary>
        /// 执行方法(异步)
        /// </summary>
        /// <typeparam name="TResult">返回值类型</typeparam>
        /// <param name="func">func</param>
        /// <returns></returns>
        public async Task<TResult> ExecuteAsync<TResult>(Func<TResult> func)
        {
            var count = 0;
            var exceptions = new List<Exception>();
            while (true)
            {
                try
                {
                    await Task.Run<TResult>(func);
                }
                catch (Exception ex)
                {
                    count++;
                    times--;
                    if (reject != null) reject(count, ex);
                    if (times <= 0)
                    {
                        var tcs = new TaskCompletionSource<TResult>();
                        tcs.SetResult(default(TResult));
                        tcs.SetException(new AggregateException(exceptions));
                        await tcs.Task;
                    }
                    if (cachers.Keys.Contains(ex.GetType())) exceptions.Add(ex);
                    Thread.Sleep(interval);
                }
            }
        }
    }
}
