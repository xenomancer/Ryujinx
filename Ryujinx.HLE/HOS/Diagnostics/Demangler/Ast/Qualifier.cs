using System.IO;

namespace Ryujinx.HLE.HOS.Diagnostics.Demangler.Ast
{
    public enum CV
    {
        None,
        Const,
        Volatile,
        Restricted = 4
    }

    public enum Reference
    {
        None,
        RValue,
        LValue
    }

    public class CVType : ParentNode
    {
        public CV Qualifier;

        public CVType(CV qualifier, BaseNode child) : base(NodeType.CvQualifierType, child)
        {
            Qualifier = qualifier;
        }

        public void PrintQualifier(TextWriter writer)
        {
            if ((Qualifier & CV.Const) != 0) writer.Write(" const");

            if ((Qualifier & CV.Volatile) != 0) writer.Write(" volatile");

            if ((Qualifier & CV.Restricted) != 0) writer.Write(" restrict");
        }

        public override void PrintLeft(TextWriter writer)
        {
            if (Child != null) Child.PrintLeft(writer);

            PrintQualifier(writer);
        }

        public override bool HasRightPart()
        {
            return Child != null && Child.HasRightPart();
        }

        public override void PrintRight(TextWriter writer)
        {
            if (Child != null) Child.PrintRight(writer);
        }
    }

    public class SimpleReferenceType : ParentNode
    {
        public Reference Qualifier;

        public SimpleReferenceType(Reference qualifier, BaseNode child) : base(NodeType.SimpleReferenceType, child)
        {
            Qualifier = qualifier;
        }

        public void PrintQualifier(TextWriter writer)
        {
            if ((Qualifier & Reference.LValue) != 0) writer.Write("&");

            if ((Qualifier & Reference.RValue) != 0) writer.Write("&&");
        }

        public override void PrintLeft(TextWriter writer)
        {
            if (Child != null)
                Child.PrintLeft(writer);
            else if (Qualifier != Reference.None) writer.Write(" ");

            PrintQualifier(writer);
        }

        public override bool HasRightPart()
        {
            return Child != null && Child.HasRightPart();
        }

        public override void PrintRight(TextWriter writer)
        {
            if (Child != null) Child.PrintRight(writer);
        }
    }
}