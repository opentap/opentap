//            Copyright Keysight Technologies 2012-2019
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, you can obtain one at http://mozilla.org/MPL/2.0/.
using System;
using System.Collections.Generic;
using System.Text;

namespace OpenTap
{
    internal enum LicenseToken
    {
        EOF,
        Error,

        LParan, RParan,

        Identifier,
        Process,

        And,
        Or
    }

    internal class TokenEntry
    {
        public LicenseToken Token;
        public int Position;
        public int Length;
        public string Text;

        public TokenEntry(LicenseToken token, string tokenStr, int position)
        {
            Token = token;
            Text = tokenStr;
            Position = position;
            Length = tokenStr.Length;
        }

        public TokenEntry(LicenseToken token, string tokenStr, int position, int length)
        {
            Token = token;
            Text = tokenStr;
            Position = position;
            Length = length;
        }
    }

    internal class Scanner
    {
        string Str;
        int idx = -1;
        char ch = '\0';

        bool EOF { get { return idx >= Str.Length; } }

        public List<TokenEntry> Tokens { get; private set; }

        private void AddToken(LicenseToken Token, string TokenStr = "", int Position = -1, int EndPosition = -1)
        {
            if (EndPosition >= 0)
                Tokens.Add(new TokenEntry(Token, TokenStr, Position, EndPosition - Position));
            else
                Tokens.Add(new TokenEntry(Token, TokenStr, Position));
        }

        private void GetChar()
        {
            idx++;
            if (EOF)
            {
                ch = '\0';
                return;
            }
            else
                ch = Str[idx];
        }

        private void Trim()
        {
            while ((!EOF) && ((int)ch <= 32))
            {
                if (EOF) return;

                GetChar();
            }
        }

        private void Tokenize()
        {
            while (!EOF)
            {
                Trim();

                if (EOF) break;

                int StartIndex = idx;

                if (ch == '(')
                {
                    AddToken(LicenseToken.LParan, "(", StartIndex);
                    GetChar();
                }
                else if (ch == ')')
                {
                    AddToken(LicenseToken.RParan, ")", StartIndex);
                    GetChar();
                }
                else if (ch == '&')
                {
                    AddToken(LicenseToken.And, "&", StartIndex);
                    GetChar();
                }
                else if (ch == '|')
                {
                    AddToken(LicenseToken.Or, "|", StartIndex);
                    GetChar();
                }
                else if (ch == '"')
                {
                    string st = "";
                    GetChar();

                    while (ch != '"')
                    {
                        if (EOF)
                        {
                            Error("String exceeded end of line");
                            break;
                        }

                        st += ch;
                        GetChar();
                    }

                    AddToken(LicenseToken.Identifier, st, StartIndex, idx);

                    GetChar();
                }
                else if (ch == '{')
                {
                    string st = "";
                    GetChar();

                    while (ch != '}')
                    {
                        if (EOF)
                        {
                            Error("String exceeded end of line");
                            break;
                        }

                        st += ch;
                        GetChar();
                    }

                    AddToken(LicenseToken.Process, st, StartIndex, idx);

                    GetChar();
                }
                else if (char.IsLetterOrDigit(ch))
                {
                    string st = "";

                    while ((char.IsLetterOrDigit(ch) || (ch=='_') || (ch == '-')) && !EOF)
                    {
                        st += ch;
                        GetChar();
                    }

                    AddToken(LicenseToken.Identifier, st, StartIndex);
                }
                else
                {
                    Error("Cannot parse expression");

                    GetChar();
                }
            }
        }

        private void Error(string v)
        {
            AddToken(LicenseToken.Error, v);
        }

        public Scanner(string Input)
        {
            Tokens = new List<TokenEntry>();
            
            Str = Input;
            GetChar();

            Tokenize();
        }
    }

}
