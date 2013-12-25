using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Remoting.Contexts;
using System.Runtime.Remoting.Activation;
using System.Runtime.Remoting.Messaging;
using System.Collections;

namespace AOPDemo
{
    class Program
    {
        static void Main(string[] args)
        {
            Calculator cal = new Calculator();
            cal.Add(3, 5);
            cal.Substract(3, 5);
            Console.ReadLine();
        }
    }

    [LogAOP]
    public class Calculator : ContextBoundObject
    {
        public int Add(int x, int y)
        {
            return x + y;
        }
        public int Substract(int x, int y)
        {
            return x - y;
        }
    }

    #region AOP base
    public interface IBeforeAdvice
    {
        void BeforeAdvice(IMethodCallMessage callMsg);
    }
    public interface IAfterAdvice
    {
        void AfterAdvice(IMethodReturnMessage callMsg);
    }

    public abstract class AOPAttribute : Attribute,IContextAttribute
    {
        private string m_aspectXml;
        private const string CONFIGFILE = @"configuration\aspect.xml";

        public AOPAttribute()
        {
            m_aspectXml = CONFIGFILE;
        }

        public AOPAttribute(string aspectfile)
        {
            m_aspectXml = aspectfile;
        }

        protected abstract AOPProperty GetAOPProperty();


        #region IContextAttribute Members

        public void GetPropertiesForNewContext(IConstructionCallMessage msg)
        {
            AOPProperty property = GetAOPProperty();
            property.AspectXml = m_aspectXml;
            msg.ContextProperties.Add(property);
        }

        public bool IsContextOK(Context ctx, IConstructionCallMessage msg)
        {
            return false;
        }

        #endregion
    }

    public abstract class AOPProperty : IContextProperty, IContributeServerContextSink
    {
        private string m_aspectXml;

        public AOPProperty()
        {
            m_aspectXml = string.Empty;
        }

        public string AspectXml
        {
            set { m_aspectXml = value; }
        }

        protected abstract IMessageSink CreateAspect(IMessageSink nextSink);
        protected virtual string GetName()
        {
            return "AOP";
        }
        protected virtual void FreezeImpl(Context newContext)
        {
            return;
        }
        protected virtual bool CheckNewContext(Context newContext)
        {
            return true;
        }

        #region IContextProperty Members

        public void Freeze(Context newContext)
        {
            FreezeImpl(newContext);
        }

        public bool IsNewContextOK(Context newCtx)
        {
            return CheckNewContext(newCtx);
        }

        public string Name
        {
            get { return GetName(); }
        }

        #endregion

        #region IContributeServerContextSink Members

        public IMessageSink GetServerContextSink(IMessageSink nextSink)
        {
            Aspect aspect = (Aspect)CreateAspect(nextSink);
            aspect.ReadAspect(m_aspectXml, Name);
            return (IMessageSink)aspect;
        }

        #endregion
    }

    public abstract class Aspect : IMessageSink
    {
        private IMessageSink m_NextSink;

        private SortedList m_BeforeAdvices = new SortedList();
        private SortedList m_AfterAdvices = new SortedList();

        public Aspect(IMessageSink nextSink)
        {
            m_NextSink = nextSink;
        }

        protected virtual void AddBeforeAdvice(string methodName, IBeforeAdvice before)
        {
            lock (this.m_BeforeAdvices)
            {
                if (!m_BeforeAdvices.Contains(methodName))
                {
                    m_BeforeAdvices.Add(methodName, before);
                }
            }
        }
        protected virtual void AddAfterAdvice(string methodName, IAfterAdvice after)
        {
            lock (this.m_AfterAdvices)
            {
                if (!m_AfterAdvices.Contains(methodName))
                {
                    m_AfterAdvices.Add(methodName, after);
                }
            }
        }

        public IBeforeAdvice FindBeforeAdvice(string methodName)
        {
            IBeforeAdvice before;
            lock (this.m_BeforeAdvices)
            {
                before = (IBeforeAdvice)m_BeforeAdvices[methodName];
            }
            return before;
        }
        public IAfterAdvice FindAfterAdvice(string methodName)
        {
            IAfterAdvice after;
            lock (this.m_AfterAdvices)
            {
                after = (IAfterAdvice)m_AfterAdvices[methodName];
            }
            return after;
        }

        public virtual void ReadAspect(string aspectXml, string aspectName)
        {
            
        }

        #region IMessageSink Members

        public IMessageCtrl AsyncProcessMessage(IMessage msg, IMessageSink replySink)
        {
            return null;
        }

        public IMessageSink NextSink
        {
            get { return m_NextSink; }
        }

        public IMessage SyncProcessMessage(IMessage msg)
        {
            IMethodCallMessage call = msg as IMethodCallMessage;
            if (call == null)
            {
                return null;
            }
            string methodName = call.MethodName.ToUpper();
            IBeforeAdvice before = FindBeforeAdvice(methodName);
            if (before != null)
            {
                before.BeforeAdvice(call);
            }
            
            IMessage retMsg = m_NextSink.SyncProcessMessage(msg);
            IMethodReturnMessage reply = retMsg as IMethodReturnMessage;

            IAfterAdvice after = FindAfterAdvice(methodName);
            if (after != null)
            {
                after.AfterAdvice(reply);
            }
            return retMsg;
        }

        #endregion

        private void BeforeProcess()
        { 
        }
        private void AfterProcess()
        { 
        }
    }
    #endregion

    #region Log AOP
    [AttributeUsage(AttributeTargets.Class)]
    public class LogAOPAttribute : AOPAttribute
    {
        public LogAOPAttribute()
            : base()
        {
        }

        public LogAOPAttribute(string aspectXml)
            : base(aspectXml)
        { 
        }

        protected override AOPProperty GetAOPProperty()
        {
            return new LogAOPProperty();
        }
    }

    public class LogAOPProperty : AOPProperty
    {
        protected override IMessageSink CreateAspect(IMessageSink nextSink)
        {
            return new LogAspect(nextSink);
        }
        protected override string GetName()
        {
            return "LogAOP";
        }
    }
    public class LogAspect : Aspect
    {
        public LogAspect(IMessageSink nextSink)
            : base(nextSink)
        {
        }

        public override void ReadAspect(string aspectXml, string aspectName)
        {
            base.ReadAspect(aspectXml, aspectName);
            AddBeforeAdvice("ADD", new LogAdvice());
            AddBeforeAdvice("SUBSTRACT", new LogAdvice());
            AddAfterAdvice("ADD", new LogAdvice());
            AddAfterAdvice("SUBSTRACT", new LogAdvice());
        }
    }
    public class LogAdvice : IBeforeAdvice, IAfterAdvice
    {
        #region IBeforeAdvice Members

        public void BeforeAdvice(IMethodCallMessage callMsg)
        {
            if (callMsg == null)
            {
                return;
            }
            Console.WriteLine("{0}({1},{2})",callMsg.MethodName,callMsg.GetArg(0),callMsg.GetArg(1));
        }

        #endregion

        #region IAfterAdvice Members

        public void AfterAdvice(IMethodReturnMessage callMsg)
        {
            if (callMsg == null)
            {
                return;
            }
            Console.WriteLine("Result is {0}", callMsg.ReturnValue);
        }

        #endregion
    }
    #endregion
}
