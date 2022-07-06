using NUnit.Framework;
using OpenTap.Authentication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenTap.UnitTests
{
    public class AuthenticationTests
    {
        [Test]
        public void ParseTokens()
        {
            string response = @"{
    ""access_token"": ""eyJhbGciOiJSUzI1NiIsInR5cCIgOiAiSldUIiwia2lkIiA6ICJIaHlId25IdDRnclRDYVhtRHNlSVhHX2U3ajVNb3YzakhLTjZWVlZsZ0lNIn0.eyJleHAiOjE2NDg3MTEwMDQsImlhdCI6MTY0ODcxMDcwNCwianRpIjoiM2NkZDRkYzEtMGE2Mi00YzBjLTljNzQtMmFhZTUwNDk2YWM1IiwiaXNzIjoiaHR0cHM6Ly9rZXljbG9hay5rczg1MDAuYWxiLmlzLmtleXNpZ2h0LmNvbS9hdXRoL3JlYWxtcy9rczg1MDAiLCJhdWQiOiJhY2NvdW50Iiwic3ViIjoiMWY1NmRkMGItNzFkOS00MjAwLWI0YzYtZDE5NWNlNGUxYzJiIiwidHlwIjoiQmVhcmVyIiwiYXpwIjoiZGVubmlzIiwic2Vzc2lvbl9zdGF0ZSI6IjA1ZDdhNmYyLTcwZGQtNDhlMy04MDZiLWRjOWNjYjVlN2YzZSIsImFjciI6IjEiLCJyZWFsbV9hY2Nlc3MiOnsicm9sZXMiOlsiZGVmYXVsdC1yb2xlcy1rczg1MDAiLCJvZmZsaW5lX2FjY2VzcyIsInVtYV9hdXRob3JpemF0aW9uIl19LCJyZXNvdXJjZV9hY2Nlc3MiOnsiYWNjb3VudCI6eyJyb2xlcyI6WyJtYW5hZ2UtYWNjb3VudCIsIm1hbmFnZS1hY2NvdW50LWxpbmtzIiwidmlldy1wcm9maWxlIl19fSwic2NvcGUiOiJvcGVuaWQgcHJvZmlsZSBlbWFpbCIsInNpZCI6IjA1ZDdhNmYyLTcwZGQtNDhlMy04MDZiLWRjOWNjYjVlN2YzZSIsImNsaWVudElkIjoiZGVubmlzIiwiY2xpZW50SG9zdCI6IjEwLjE0OS4xMDkuMjUyIiwiZW1haWxfdmVyaWZpZWQiOmZhbHNlLCJwcmVmZXJyZWRfdXNlcm5hbWUiOiJzZXJ2aWNlLWFjY291bnQtZGVubmlzIiwiY2xpZW50QWRkcmVzcyI6IjEwLjE0OS4xMDkuMjUyIn0.GWKY9EV4sqpLg0GGfmqnGcfjBGhN2NunJrfIysaeRJaYnfsmo3rt-_awpbg15q6IXFipr8N6kE965Y0rxeODxAmRVIf8pb-GkaT0qMOpUidiZrUz3FC0WDXymH3gBayOaKOIa03qVOn5fURmGV4nbQyuJemgQYXW8fcFQpu8xrsM9leYGzVXU4zdxNR-jSfYq1iNN2je9E-EhlglxmvQnirRcoGJsymLxg0s6M_s6cQnQBOihuKsEPE8C3zBeVoCXYJ3kkY0Q6GGK2e8EoRhvrNQ-pyK58yJv5YEnC6Erxe06tfFBZ_XX596YerAqkI4XlHpuEBddqGP0HSYlwtcjA"",
    ""expires_in"": 300,
    ""refresh_expires_in"": 1800,
    ""refresh_token"": ""eyJhbGciOiJIUzI1NiIsInR5cCIgOiAiSldUIiwia2lkIiA6ICJmMjk4MTY1ZS01MDE5LTQ2YjQtYTYyNS1hMzhiMGY0MzUwZDgifQ.eyJleHAiOjE2NDg3MTI1MDQsImlhdCI6MTY0ODcxMDcwNCwianRpIjoiOTdhOGE3YmEtMTU0OS00NWU5LTgxN2YtY2E1M2YyNzkzYjcwIiwiaXNzIjoiaHR0cHM6Ly9rZXljbG9hay5rczg1MDAuYWxiLmlzLmtleXNpZ2h0LmNvbS9hdXRoL3JlYWxtcy9rczg1MDAiLCJhdWQiOiJodHRwczovL2tleWNsb2FrLmtzODUwMC5hbGIuaXMua2V5c2lnaHQuY29tL2F1dGgvcmVhbG1zL2tzODUwMCIsInN1YiI6IjFmNTZkZDBiLTcxZDktNDIwMC1iNGM2LWQxOTVjZTRlMWMyYiIsInR5cCI6IlJlZnJlc2giLCJhenAiOiJkZW5uaXMiLCJzZXNzaW9uX3N0YXRlIjoiMDVkN2E2ZjItNzBkZC00OGUzLTgwNmItZGM5Y2NiNWU3ZjNlIiwic2NvcGUiOiJvcGVuaWQgcHJvZmlsZSBlbWFpbCIsInNpZCI6IjA1ZDdhNmYyLTcwZGQtNDhlMy04MDZiLWRjOWNjYjVlN2YzZSJ9.VftS1sPVoP_vrYBRuOy7ZT-J9L8SCofxUH1L_5pI6FU"",
    ""token_type"": ""Bearer"",
    ""id_token"": ""eyJhbGciOiJSUzI1NiIsInR5cCIgOiAiSldUIiwia2lkIiA6ICJIaHlId25IdDRnclRDYVhtRHNlSVhHX2U3ajVNb3YzakhLTjZWVlZsZ0lNIn0.eyJleHAiOjE2NDg3MTEwMDQsImlhdCI6MTY0ODcxMDcwNCwiYXV0aF90aW1lIjowLCJqdGkiOiIwNDQ1MjJmZi1iZWZmLTQxOWYtOTcyNC0yNWJmMDY5ZTdjMjgiLCJpc3MiOiJodHRwczovL2tleWNsb2FrLmtzODUwMC5hbGIuaXMua2V5c2lnaHQuY29tL2F1dGgvcmVhbG1zL2tzODUwMCIsImF1ZCI6ImRlbm5pcyIsInN1YiI6IjFmNTZkZDBiLTcxZDktNDIwMC1iNGM2LWQxOTVjZTRlMWMyYiIsInR5cCI6IklEIiwiYXpwIjoiZGVubmlzIiwic2Vzc2lvbl9zdGF0ZSI6IjA1ZDdhNmYyLTcwZGQtNDhlMy04MDZiLWRjOWNjYjVlN2YzZSIsImF0X2hhc2giOiJvMElRSnRyYW52NjA0RUFuWVFETUVnIiwiYWNyIjoiMSIsInNpZCI6IjA1ZDdhNmYyLTcwZGQtNDhlMy04MDZiLWRjOWNjYjVlN2YzZSIsImNsaWVudElkIjoiZGVubmlzIiwiY2xpZW50SG9zdCI6IjEwLjE0OS4xMDkuMjUyIiwiZW1haWxfdmVyaWZpZWQiOmZhbHNlLCJwcmVmZXJyZWRfdXNlcm5hbWUiOiJzZXJ2aWNlLWFjY291bnQtZGVubmlzIiwiY2xpZW50QWRkcmVzcyI6IjEwLjE0OS4xMDkuMjUyIn0.JgqiDXT0aArwz0P6nbN8c6HPuVsmUHPuXeCFEsTjf3VdN0PjX2j8thrkGmBw6dr5bSwpX0LTP1vxZfspIe-rpi5UuiKCAmkY92M8T5A7m3yI8gDvlA3RzjXAnrTu3it436444YpYwV9PlQZipy7pIaaWqDOP4AJbnhGARWNHTMozSBCClmQva50nzjEGFFI4Z2ZI9-SPbETdY1xxAqept7JMLnJuPRw_BXFQc8oYTGnqr7kBm8mQnWoFbFi1DZk7VP7e0sSYaI9H3SWZgc2jNpiAa4tpDx9GnTVV4mtQjxB2Xvl_KwZirszewdnDo83M4TnV79PzIr3aJAjOD7crDw"",
    ""not-before-policy"": 0,
    ""session_state"": ""05d7a6f2-70dd-48e3-806b-dc9ccb5e7f3e"",
    ""scope"": ""openid profile email""
}";
            var ti = TokenInfo.FromResponse(response, "http://packages.opentap.io");
            Assert.IsNotNull(ti.RefreshToken);


            response = @"{
    ""access_token"": ""eyJhbGciOiJSUzI1NiIsInR5cCIgOiAiSldUIiwia2lkIiA6ICJIaHlId25IdDRnclRDYVhtRHNlSVhHX2U3ajVNb3YzakhLTjZWVlZsZ0lNIn0.eyJleHAiOjE2NDg3MTEwMDQsImlhdCI6MTY0ODcxMDcwNCwianRpIjoiM2NkZDRkYzEtMGE2Mi00YzBjLTljNzQtMmFhZTUwNDk2YWM1IiwiaXNzIjoiaHR0cHM6Ly9rZXljbG9hay5rczg1MDAuYWxiLmlzLmtleXNpZ2h0LmNvbS9hdXRoL3JlYWxtcy9rczg1MDAiLCJhdWQiOiJhY2NvdW50Iiwic3ViIjoiMWY1NmRkMGItNzFkOS00MjAwLWI0YzYtZDE5NWNlNGUxYzJiIiwidHlwIjoiQmVhcmVyIiwiYXpwIjoiZGVubmlzIiwic2Vzc2lvbl9zdGF0ZSI6IjA1ZDdhNmYyLTcwZGQtNDhlMy04MDZiLWRjOWNjYjVlN2YzZSIsImFjciI6IjEiLCJyZWFsbV9hY2Nlc3MiOnsicm9sZXMiOlsiZGVmYXVsdC1yb2xlcy1rczg1MDAiLCJvZmZsaW5lX2FjY2VzcyIsInVtYV9hdXRob3JpemF0aW9uIl19LCJyZXNvdXJjZV9hY2Nlc3MiOnsiYWNjb3VudCI6eyJyb2xlcyI6WyJtYW5hZ2UtYWNjb3VudCIsIm1hbmFnZS1hY2NvdW50LWxpbmtzIiwidmlldy1wcm9maWxlIl19fSwic2NvcGUiOiJvcGVuaWQgcHJvZmlsZSBlbWFpbCIsInNpZCI6IjA1ZDdhNmYyLTcwZGQtNDhlMy04MDZiLWRjOWNjYjVlN2YzZSIsImNsaWVudElkIjoiZGVubmlzIiwiY2xpZW50SG9zdCI6IjEwLjE0OS4xMDkuMjUyIiwiZW1haWxfdmVyaWZpZWQiOmZhbHNlLCJwcmVmZXJyZWRfdXNlcm5hbWUiOiJzZXJ2aWNlLWFjY291bnQtZGVubmlzIiwiY2xpZW50QWRkcmVzcyI6IjEwLjE0OS4xMDkuMjUyIn0.GWKY9EV4sqpLg0GGfmqnGcfjBGhN2NunJrfIysaeRJaYnfsmo3rt-_awpbg15q6IXFipr8N6kE965Y0rxeODxAmRVIf8pb-GkaT0qMOpUidiZrUz3FC0WDXymH3gBayOaKOIa03qVOn5fURmGV4nbQyuJemgQYXW8fcFQpu8xrsM9leYGzVXU4zdxNR-jSfYq1iNN2je9E-EhlglxmvQnirRcoGJsymLxg0s6M_s6cQnQBOihuKsEPE8C3zBeVoCXYJ3kkY0Q6GGK2e8EoRhvrNQ-pyK58yJv5YEnC6Erxe06tfFBZ_XX596YerAqkI4XlHpuEBddqGP0HSYlwtcjA"",
    ""expires_in"": 300,
    ""refresh_expires_in"": 1800,
    ""refresh_token"": ""eyJhbGciOiJIUzI1NiIsInR5cCIgOiAiSldUIiwia2lkIiA6ICJmMjk4MTY1ZS01MDE5LTQ2YjQtYTYyNS1hMzhiMGY0MzUwZDgifQ.eyJleHAiOjE2NDg3MTI1MDQsImlhdCI6MTY0ODcxMDcwNCwianRpIjoiOTdhOGE3YmEtMTU0OS00NWU5LTgxN2YtY2E1M2YyNzkzYjcwIiwiaXNzIjoiaHR0cHM6Ly9rZXljbG9hay5rczg1MDAuYWxiLmlzLmtleXNpZ2h0LmNvbS9hdXRoL3JlYWxtcy9rczg1MDAiLCJhdWQiOiJodHRwczovL2tleWNsb2FrLmtzODUwMC5hbGIuaXMua2V5c2lnaHQuY29tL2F1dGgvcmVhbG1zL2tzODUwMCIsInN1YiI6IjFmNTZkZDBiLTcxZDktNDIwMC1iNGM2LWQxOTVjZTRlMWMyYiIsInR5cCI6IlJlZnJlc2giLCJhenAiOiJkZW5uaXMiLCJzZXNzaW9uX3N0YXRlIjoiMDVkN2E2ZjItNzBkZC00OGUzLTgwNmItZGM5Y2NiNWU3ZjNlIiwic2NvcGUiOiJvcGVuaWQgcHJvZmlsZSBlbWFpbCIsInNpZCI6IjA1ZDdhNmYyLTcwZGQtNDhlMy04MDZiLWRjOWNjYjVlN2YzZSJ9.VftS1sPVoP_vrYBRuOy7ZT-J9L8SCofxUH1L_5pI6FU"",
    ""token_type"": ""Bearer"",
    ""not-before-policy"": 0,
    ""session_state"": ""05d7a6f2-70dd-48e3-806b-dc9ccb5e7f3e"",
    ""scope"": ""openid profile email""
}";
            ti = TokenInfo.FromResponse(response, "http://packages.opentap.io");
            Assert.IsNotNull(ti.RefreshToken);

            response = @"{
    ""access_token"": ""eyJhbGciOiJSUzI1NiIsInR5cCIgOiAiSldUIiwia2lkIiA6ICJIaHlId25IdDRnclRDYVhtRHNlSVhHX2U3ajVNb3YzakhLTjZWVlZsZ0lNIn0.eyJleHAiOjE2NDg3MTEwMDQsImlhdCI6MTY0ODcxMDcwNCwianRpIjoiM2NkZDRkYzEtMGE2Mi00YzBjLTljNzQtMmFhZTUwNDk2YWM1IiwiaXNzIjoiaHR0cHM6Ly9rZXljbG9hay5rczg1MDAuYWxiLmlzLmtleXNpZ2h0LmNvbS9hdXRoL3JlYWxtcy9rczg1MDAiLCJhdWQiOiJhY2NvdW50Iiwic3ViIjoiMWY1NmRkMGItNzFkOS00MjAwLWI0YzYtZDE5NWNlNGUxYzJiIiwidHlwIjoiQmVhcmVyIiwiYXpwIjoiZGVubmlzIiwic2Vzc2lvbl9zdGF0ZSI6IjA1ZDdhNmYyLTcwZGQtNDhlMy04MDZiLWRjOWNjYjVlN2YzZSIsImFjciI6IjEiLCJyZWFsbV9hY2Nlc3MiOnsicm9sZXMiOlsiZGVmYXVsdC1yb2xlcy1rczg1MDAiLCJvZmZsaW5lX2FjY2VzcyIsInVtYV9hdXRob3JpemF0aW9uIl19LCJyZXNvdXJjZV9hY2Nlc3MiOnsiYWNjb3VudCI6eyJyb2xlcyI6WyJtYW5hZ2UtYWNjb3VudCIsIm1hbmFnZS1hY2NvdW50LWxpbmtzIiwidmlldy1wcm9maWxlIl19fSwic2NvcGUiOiJvcGVuaWQgcHJvZmlsZSBlbWFpbCIsInNpZCI6IjA1ZDdhNmYyLTcwZGQtNDhlMy04MDZiLWRjOWNjYjVlN2YzZSIsImNsaWVudElkIjoiZGVubmlzIiwiY2xpZW50SG9zdCI6IjEwLjE0OS4xMDkuMjUyIiwiZW1haWxfdmVyaWZpZWQiOmZhbHNlLCJwcmVmZXJyZWRfdXNlcm5hbWUiOiJzZXJ2aWNlLWFjY291bnQtZGVubmlzIiwiY2xpZW50QWRkcmVzcyI6IjEwLjE0OS4xMDkuMjUyIn0.GWKY9EV4sqpLg0GGfmqnGcfjBGhN2NunJrfIysaeRJaYnfsmo3rt-_awpbg15q6IXFipr8N6kE965Y0rxeODxAmRVIf8pb-GkaT0qMOpUidiZrUz3FC0WDXymH3gBayOaKOIa03qVOn5fURmGV4nbQyuJemgQYXW8fcFQpu8xrsM9leYGzVXU4zdxNR-jSfYq1iNN2je9E-EhlglxmvQnirRcoGJsymLxg0s6M_s6cQnQBOihuKsEPE8C3zBeVoCXYJ3kkY0Q6GGK2e8EoRhvrNQ-pyK58yJv5YEnC6Erxe06tfFBZ_XX596YerAqkI4XlHpuEBddqGP0HSYlwtcjA"",
    ""expires_in"": 300,
    ""token_type"": ""Bearer"",
    ""not-before-policy"": 0,
    ""session_state"": ""05d7a6f2-70dd-48e3-806b-dc9ccb5e7f3e"",
    ""scope"": ""openid profile email""
}";
            ti = TokenInfo.FromResponse(response, "http://packages.opentap.io");
            Assert.IsNull(ti.RefreshToken);
        }

        [Test]
        public void TokenExpiration()
        {

            string response = @"{
    ""access_token"": ""eyJhbGciOiJSUzI1NiIsInR5cCIgOiAiSldUIiwia2lkIiA6ICJNTGNwamN3ck9uZnNHZWFheXZ1UVppaVZ5RjRhREVweE1XdU1mdGdScU5rIn0.eyJleHAiOjE2NTcwMTg5NzYsImlhdCI6MTY1NzAxODY3NiwianRpIjoiN2RiNDkzM2UtZGIzOS00MTlhLTgxNTctMzBlOTc0YTc2M2JkIiwiaXNzIjoiaHR0cHM6Ly9rZXljbG9hay5rczg1MDAuYWxiLmlzLmtleXNpZ2h0LmNvbS9hdXRoL3JlYWxtcy9rczg1MDAiLCJhdWQiOlsiY2x0LWtzODUwMC1zZWVkIiwiYWNjb3VudCJdLCJzdWIiOiIxZWI4YmFmNC02OWUwLTQyYTQtYTNlMS0yYmM5ZjZhNGUwNzciLCJ0eXAiOiJCZWFyZXIiLCJhenAiOiJjbHQta3M4NTAwLXNlc3Npb24tbWFuYWdlciIsInNlc3Npb25fc3RhdGUiOiIzYzQyYzM5NS1iMDEyLTRhMWMtYTc5YS00NGY1MWQzMzhkOGUiLCJhY3IiOiIxIiwicmVhbG1fYWNjZXNzIjp7InJvbGVzIjpbImRlZmF1bHQtcm9sZXMta3M4NTAwIiwib2ZmbGluZV9hY2Nlc3MiLCJ1bWFfYXV0aG9yaXphdGlvbiJdfSwicmVzb3VyY2VfYWNjZXNzIjp7ImFjY291bnQiOnsicm9sZXMiOlsibWFuYWdlLWFjY291bnQiLCJtYW5hZ2UtYWNjb3VudC1saW5rcyIsInZpZXctcHJvZmlsZSJdfX0sInNjb3BlIjoib3BlbmlkIHByb2ZpbGUgZW1haWwiLCJzaWQiOiIzYzQyYzM5NS1iMDEyLTRhMWMtYTc5YS00NGY1MWQzMzhkOGUiLCJlbWFpbF92ZXJpZmllZCI6dHJ1ZSwicHJlZmVycmVkX3VzZXJuYW1lIjoiZGV2ZWxvcGVyIn0.AH34kCBwfPbqpXHevgyatejxtIvwZCw11wae_P_1qSKP-1EAhSYDqpepINycnORL7PBqEcbq841ohp-Ih-kPVN7K8MLDIPkHZM-FbTh5BxddjlQT0f_O7inglpwDtNNRCGr3gvVClv4eQJ1HPktyeispiFfLqlXvrDgD9_I4TPuRqRSa_fmY2wGDrMtIFSPWn1DGdpV7_-_vOxMs1eJGPH71Ghvlkhgkywccd3Gvl6RPA8L8wq4bDIlJ4v-5LoyBnznZJEalb4lPiEgOhxHLmRjUEcmXUcKhG7uaf7EgN61XqNhmVBVuTLM3gCH44EFSsNER9B3_6Am1SM_z8P1Lhg"",
    ""expires_in"": 299,
    ""refresh_expires_in"": 1800,
    ""refresh_token"": ""eyJhbGciOiJIUzI1NiIsInR5cCIgOiAiSldUIiwia2lkIiA6ICI5NzgwMjU4Yi05MTVhLTQ3YzctYTA1Ni02YTkyOGY0MTc1MzkifQ.eyJleHAiOjE2NTcwMjA0NzcsImlhdCI6MTY1NzAxODY3NywianRpIjoiYzQ1ZmJlMGEtZmQ0Zi00ZTA3LWEyNTItODYwZTJjNzI5ZjA3IiwiaXNzIjoiaHR0cHM6Ly9rZXljbG9hay5rczg1MDAuYWxiLmlzLmtleXNpZ2h0LmNvbS9hdXRoL3JlYWxtcy9rczg1MDAiLCJhdWQiOiJodHRwczovL2tleWNsb2FrLmtzODUwMC5hbGIuaXMua2V5c2lnaHQuY29tL2F1dGgvcmVhbG1zL2tzODUwMCIsInN1YiI6IjFlYjhiYWY0LTY5ZTAtNDJhNC1hM2UxLTJiYzlmNmE0ZTA3NyIsInR5cCI6IlJlZnJlc2giLCJhenAiOiJjbHQta3M4NTAwLXNlc3Npb24tbWFuYWdlciIsInNlc3Npb25fc3RhdGUiOiIzYzQyYzM5NS1iMDEyLTRhMWMtYTc5YS00NGY1MWQzMzhkOGUiLCJzY29wZSI6Im9wZW5pZCBwcm9maWxlIGVtYWlsIiwic2lkIjoiM2M0MmMzOTUtYjAxMi00YTFjLWE3OWEtNDRmNTFkMzM4ZDhlIn0.OlBx9v0iRviNuCjcYM42SzvrKcpKfGgWpDE-Brf5hvc"",
    ""token_type"": ""Bearer"",
    ""id_token"": ""eyJhbGciOiJSUzI1NiIsInR5cCIgOiAiSldUIiwia2lkIiA6ICJNTGNwamN3ck9uZnNHZWFheXZ1UVppaVZ5RjRhREVweE1XdU1mdGdScU5rIn0.eyJleHAiOjE2NTcwMTg5NzYsImlhdCI6MTY1NzAxODY3NywiYXV0aF90aW1lIjowLCJqdGkiOiI3ZmM2YjBkZC1iOTBkLTQyOGQtODkwMS1iYzliN2JmZGQ3ZGYiLCJpc3MiOiJodHRwczovL2tleWNsb2FrLmtzODUwMC5hbGIuaXMua2V5c2lnaHQuY29tL2F1dGgvcmVhbG1zL2tzODUwMCIsImF1ZCI6WyJjbHQta3M4NTAwLXNlc3Npb24tbWFuYWdlciIsImNsdC1rczg1MDAtc2VlZCJdLCJzdWIiOiIxZWI4YmFmNC02OWUwLTQyYTQtYTNlMS0yYmM5ZjZhNGUwNzciLCJ0eXAiOiJJRCIsImF6cCI6ImNsdC1rczg1MDAtc2Vzc2lvbi1tYW5hZ2VyIiwic2Vzc2lvbl9zdGF0ZSI6IjNjNDJjMzk1LWIwMTItNGExYy1hNzlhLTQ0ZjUxZDMzOGQ4ZSIsImF0X2hhc2giOiJTSG5fWnVteUpaU3h0WU1SZDFxcVRRIiwiYWNyIjoiMSIsInNpZCI6IjNjNDJjMzk1LWIwMTItNGExYy1hNzlhLTQ0ZjUxZDMzOGQ4ZSIsImVtYWlsX3ZlcmlmaWVkIjp0cnVlLCJwcmVmZXJyZWRfdXNlcm5hbWUiOiJkZXZlbG9wZXIifQ.ZN4zNnZ_dUeIBs51vi-EFD615J0RkVxG3eQ7Sy0Rv5dHFLE2t-xx7zLE_CXytQtbGe_JCzbmH9KbF9pPQixzuJ_Nq5fR0zHU1SwRK3eetpMIq8KjzHnbTL0SMwMuDsBevkMGIXeBry1CGLInbJHkSayq8vE7XhL0tDmd9od-9mczAeXOILX7Z2n06Iu6V8Ja6BmGYCcOsibvV94alahcmg9mIhRcr4Jlg2Bb3La1zCAHhcFU6qvuIvxLZ-gakmcgQ7EqY7zqEgsDIVc1-o1SxE7owt3qU79PYRvjv1cp6u5s3pLBO9xZZoGcKIybsEXxf6bTB6ZVQXkdnWmguHzDXQ"",
    ""not-before-policy"": 1656576311,
    ""session_state"": ""3c42c395-b012-4a1c-a79a-44f51d338d8e"",
    ""scope"": ""openid profile email""
}";

            var ti = TokenInfo.FromResponse(response, "http://packages.opentap.io");
            DateTime dateTime = new DateTime(2022, 7, 5, 11, 02, 56);
            TimeSpan timeSpan = ti.Expiration - dateTime;
            Console.WriteLine(dateTime.ToString());
            Console.WriteLine(ti.Expiration.ToString());
            Console.WriteLine(timeSpan.ToString());

            Assert.IsTrue(timeSpan.Days == 0);
            Assert.IsTrue(timeSpan.Hours == 0);
            Assert.IsTrue(timeSpan.Minutes == 0);
            Assert.IsTrue(timeSpan.Seconds == 0);
        }

        [Test]
        public void ParseTokenWithUrlChars()
        {
            // This token has a '-' and '_' in the payload which is not in the normal base64 char set. It is in the base64-URL charset though, and should work.
            string accessToken = "eyJhbGciOiJSUzI1NiIsInR5cCIgOiAiSldUIiwia2lkIiA6ICJlQUFuOTYwbFpXYU1tZGliTDg4Q29CVlhZSy1VcEhTeWE0T3Z3d04tQzI4In0.eyJleHAiOjE2NTcwNjI5MzUsImlhdCI6MTY1NzA2Mjg3NSwianRpIjoiMGYwNmM3ZWYtOWI3Yi00NzQ1LWJiNTEtY2U5ZTAzOTdmYWMzIiwiaXNzIjoiaHR0cDovL2xvY2FsaG9zdDo4MDgwL3JlYWxtcy9tYXN0ZXIiLCJhdWQiOiJhY2NvdW50Iiwic3ViIjoiM2IwNDk0NzYtMzFlOC00NjljLTk3YmEtMWEwZGFmMzAxOGMzIiwidHlwIjoiQmVhcmVyIiwiYXpwIjoicG9zdG1hbiIsInNlc3Npb25fc3RhdGUiOiI1NmU2NGQ5NC00YmFiLTRhYjYtOGZhOC1lODkzNjU2ZTlmZWIiLCJhY3IiOiIxIiwiYWxsb3dlZC1vcmlnaW5zIjpbImh0dHA6Ly9sb2NhbGhvc3QiXSwicmVhbG1fYWNjZXNzIjp7InJvbGVzIjpbImRlZmF1bHQtcm9sZXMtbWFzdGVyIiwib2ZmbGluZV9hY2Nlc3MiLCJ1bWFfYXV0aG9yaXphdGlvbiJdfSwicmVzb3VyY2VfYWNjZXNzIjp7ImFjY291bnQiOnsicm9sZXMiOlsibWFuYWdlLWFjY291bnQiLCJtYW5hZ2UtYWNjb3VudC1saW5rcyIsInZpZXctcHJvZmlsZSJdfX0sInNjb3BlIjoicHJvZmlsZSBlbWFpbCIsInNpZCI6IjU2ZTY0ZDk0LTRiYWItNGFiNi04ZmE4LWU4OTM2NTZlOWZlYiIsImVtYWlsX3ZlcmlmaWVkIjpmYWxzZSwibmFtZSI6In5-IDliODM_NTNjIiwicHJlZmVycmVkX3VzZXJuYW1lIjoiYXNnZXIiLCJnaXZlbl9uYW1lIjoifn4iLCJmYW1pbHlfbmFtZSI6IjliODM_NTNjIn0.elXy3abQHHL9-hlVOfkH1JxzgZXyiRSI8JpVJgbiFic7A9fY0qFiUC6aBrR9_FNDU7zh3A4rCAmprdbonMwFRzkRnWfnipXgPTnAtFz9q2i6M0Tcnj-AAgPvZ9sjwtKdOKyzoqoKpEsfdiFYZb31oc8M4R7dRFAixPh8ARv9Lpzx5Hnu7q7A_ewOStQWZbqD-GQvtJyslkbXJM3RaTT3VpDRSXWr67SoIff9SxcrHAJpj_gJcwrg5xrZW4IdmE87_L3LcFvaLzeUx8IvrfpmVHuR8E8yR8RMu2oxiBXD5M1LJCbD3Wx6dTszqFRUOlnR1FA4xAsSgJ8Xba4MB5PWNA";
            var t = new TokenInfo(accessToken, null, "localhost");
            Assert.AreEqual("~~ 9b83?53c", t.Claims["name"]);
            Assert.AreEqual("1", t.Claims["acr"]);
        }

        [Test]
        public void Refresh()
        {
            // This token has a '-' and '_' in the payload which is not in the normal base64 char set. It is in the base64-URL charset though, and should work.
            string accessToken = "eyJhbGciOiJSUzI1NiIsInR5cCIgOiAiSldUIiwia2lkIiA6ICItdUNOU19VdkowVEViRGhRYUdCaHBia0ZRX0oyb0FCcENWUmY5RG9RRmJvIn0.eyJleHAiOjE2NTcxMDUxODEsImlhdCI6MTY1NzEwNDU4MSwianRpIjoiZTc4YTIzODctYWM0MC00ZTY1LTlmMmUtYTBlMGRlMTIzMDBkIiwiaXNzIjoiaHR0cDovL2xvY2FsaG9zdDo4MDgwL3JlYWxtcy9tYXN0ZXIiLCJhdWQiOiJhY2NvdW50Iiwic3ViIjoiODcyMjVmNmEtM2E5Ny00Njg2LThhZGQtZThjNWY3NjlmNWZjIiwidHlwIjoiQmVhcmVyIiwiYXpwIjoidGVzdC1jbGllbnQiLCJzZXNzaW9uX3N0YXRlIjoiZWI5NjRlMTAtYjdlMS00NjllLWI2YjYtNDUwNzhkYzc2NmExIiwiYWNyIjoiMSIsImFsbG93ZWQtb3JpZ2lucyI6WyJodHRwOi8vbG9jYWxob3N0Il0sInJlYWxtX2FjY2VzcyI6eyJyb2xlcyI6WyJkZWZhdWx0LXJvbGVzLW1hc3RlciIsIm9mZmxpbmVfYWNjZXNzIiwidW1hX2F1dGhvcml6YXRpb24iXX0sInJlc291cmNlX2FjY2VzcyI6eyJhY2NvdW50Ijp7InJvbGVzIjpbIm1hbmFnZS1hY2NvdW50IiwibWFuYWdlLWFjY291bnQtbGlua3MiLCJ2aWV3LXByb2ZpbGUiXX19LCJzY29wZSI6InByb2ZpbGUgZW1haWwiLCJzaWQiOiJlYjk2NGUxMC1iN2UxLTQ2OWUtYjZiNi00NTA3OGRjNzY2YTEiLCJlbWFpbF92ZXJpZmllZCI6ZmFsc2UsInByZWZlcnJlZF91c2VybmFtZSI6ImFzZ2VyIn0.E316o1I17h9a4JO-UzTk1GOaaOOE6ES5KVn1TerBDGAI7KHyPHzcHDKNogUCLhqLvGlHPEOfzjAzX9fNRLQZz_IL75z4sJQzgvN0nWPnne24vf4IVn7TzovOYd0PTuhuGUwrBtmmSAK-VLrKPPX15KoWEyz2kQ-eeYae07giopJ0FNuyiF8Y49PYiDO7hyk00VZYe1EZCXBLa6WglK0htDEh_BZPXuHnaF00PMKym_0lnAqS5_Lk-UoTi7quvnbJmM3yfH_xNPl2mysDFxHk-E86bZ8MWudZvXHmjhlAnlAZxd-ARqAFtCImDU1Ez9nniWhg0zCpCTL9eYT0xE5bcA";
            string refreshToken = "eyJhbGciOiJIUzI1NiIsInR5cCIgOiAiSldUIiwia2lkIiA6ICI0MjVkNjFhNS00ZTU1LTQ2ZmUtOTk5OC04M2ZkMTNmYmJkYWYifQ.eyJleHAiOjE2NTcxMDYzODEsImlhdCI6MTY1NzEwNDU4MSwianRpIjoiZTVlYTBlZWItOTAyZC00YTU4LThjZmItNjE3ODQwMmEzYTI3IiwiaXNzIjoiaHR0cDovL2xvY2FsaG9zdDo4MDgwL3JlYWxtcy9tYXN0ZXIiLCJhdWQiOiJodHRwOi8vbG9jYWxob3N0OjgwODAvcmVhbG1zL21hc3RlciIsInN1YiI6Ijg3MjI1ZjZhLTNhOTctNDY4Ni04YWRkLWU4YzVmNzY5ZjVmYyIsInR5cCI6IlJlZnJlc2giLCJhenAiOiJ0ZXN0LWNsaWVudCIsInNlc3Npb25fc3RhdGUiOiJlYjk2NGUxMC1iN2UxLTQ2OWUtYjZiNi00NTA3OGRjNzY2YTEiLCJzY29wZSI6InByb2ZpbGUgZW1haWwiLCJzaWQiOiJlYjk2NGUxMC1iN2UxLTQ2OWUtYjZiNi00NTA3OGRjNzY2YTEifQ.Lp15itPOVrt5bM4nyJcy4iSlViFZ_Tc19sva7sbvnv0";
            var t = new TokenInfo(accessToken, refreshToken, "localhost");
            t.Refresh("test-client", "Gv5aQGy3YbrxrZFVjLB6MmnF4LF4f0uu");
        }

        [Test]
        public void RPT()
        {
            // This token has a '-' and '_' in the payload which is not in the normal base64 char set. It is in the base64-URL charset though, and should work.
            string accessToken = "eyJhbGciOiJSUzI1NiIsInR5cCIgOiAiSldUIiwia2lkIiA6ICItdUNOU19VdkowVEViRGhRYUdCaHBia0ZRX0oyb0FCcENWUmY5RG9RRmJvIn0.eyJleHAiOjE2NTcxMDU4MzMsImlhdCI6MTY1NzEwNTIzMywianRpIjoiZGRhNTI3MDktM2RlZi00M2UwLTgyNzItYjhmYWZjY2QxNTY1IiwiaXNzIjoiaHR0cDovL2xvY2FsaG9zdDo4MDgwL3JlYWxtcy9tYXN0ZXIiLCJhdWQiOiJhY2NvdW50Iiwic3ViIjoiODcyMjVmNmEtM2E5Ny00Njg2LThhZGQtZThjNWY3NjlmNWZjIiwidHlwIjoiQmVhcmVyIiwiYXpwIjoidGVzdC1jbGllbnQiLCJzZXNzaW9uX3N0YXRlIjoiNTNlZGNlNDQtZDc0NS00ZTJkLWE3MDQtNTBiNDVkMWUxMDFmIiwiYWNyIjoiMSIsImFsbG93ZWQtb3JpZ2lucyI6WyJodHRwOi8vbG9jYWxob3N0Il0sInJlYWxtX2FjY2VzcyI6eyJyb2xlcyI6WyJkZWZhdWx0LXJvbGVzLW1hc3RlciIsIm9mZmxpbmVfYWNjZXNzIiwidW1hX2F1dGhvcml6YXRpb24iXX0sInJlc291cmNlX2FjY2VzcyI6eyJhY2NvdW50Ijp7InJvbGVzIjpbIm1hbmFnZS1hY2NvdW50IiwibWFuYWdlLWFjY291bnQtbGlua3MiLCJ2aWV3LXByb2ZpbGUiXX19LCJzY29wZSI6InByb2ZpbGUgZW1haWwiLCJzaWQiOiI1M2VkY2U0NC1kNzQ1LTRlMmQtYTcwNC01MGI0NWQxZTEwMWYiLCJlbWFpbF92ZXJpZmllZCI6ZmFsc2UsInByZWZlcnJlZF91c2VybmFtZSI6ImFzZ2VyIn0.gmdN4yGgRcdy2g2wMZYxQslvxrit-Xp_1_1-UdlBe4HEBvi5vFStazTUBe0d7NFvbsLhv_ADpfav5N6TrgIk2EM3qLvwCv4gFNkBIibVjKo8XL6Rd0CN7bWFoeDxyWjfYAj3e0PwM151HvInPVIdPE9GsJ9_seFneBrwLZeTQh3fV39ERst1brXyN6UQyBgkjoqcCmej3rpnoPhtLoZztUvjNssoFuuiRa67F1KdSTTJQK-e51cOKA7pHqtV_U7hm7xtiIxoI5OCqkt8BebggoCJQfsPYlF8jP6NQgBLMncg4EGUtZofkUUc2q_z82ECD1puI87oRo-fEcGBIfpSiQ";
            var t = new TokenInfo(accessToken, null, "localhost");
            var rpt = t.GetRequestingPartyToken("test-client");
            Assert.AreEqual(t.Claims["sub"], rpt.Claims["sub"]);
        }


        public TokenInfo Login(string authorityUrl = "http://localhost:8080/realms/master", string user, string pass)
        {
            using (var client = new HttpClient())
            {
                var request = new HttpRequestMessage(HttpMethod.Get, authorityUrl + "/.well-known/openid-configuration");
                HttpResponseMessage response = client.SendAsync(request).Result;
                var doc = JsonDocument.Parse(response.Content.ReadAsStreamAsync().Result);
                string tokenEndoint = doc.RootElement.GetProperty("token_endpoint").GetString();
                request = new HttpRequestMessage(HttpMethod.Post, tokenEndoint);
                request.Headers.Add("Authorization", "Bearer " + AccessToken);
                var nvc = new List<KeyValuePair<string, string>>();
                nvc.Add(new KeyValuePair<string, string>("grant_type", "password"));
                nvc.Add(new KeyValuePair<string, string>("audience", audience));
                request.Content = new FormUrlEncodedContent(nvc);
                response = client.SendAsync(request).Result;
                return FromResponse(response.Content.ReadAsStringAsync().Result, Domain);
            }
        }
    }
}
