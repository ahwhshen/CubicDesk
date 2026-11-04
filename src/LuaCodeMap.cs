namespace CubicDesk {
  public static class LuaCodeMap {
    public static bool[] Create(string text){
      var code=new bool[text.Length];int i=0;
      while(i<text.Length){
        if(text[i]=='-'&&i+1<text.Length&&text[i+1]=='-'){
          int equals=LongOpen(text,i+2);if(equals>=0)i=LongEnd(text,i+2,equals);
          else {i+=2;while(i<text.Length&&text[i]!='\r'&&text[i]!='\n')i++;}continue;
        }
        if(text[i]=='"'||text[i]=='\''){
          char quote=text[i++];while(i<text.Length){char c=text[i++];if(c=='\\'&&i<text.Length)i++;else if(c==quote)break;}continue;
        }
        int level=LongOpen(text,i);if(level>=0){i=LongEnd(text,i,level);continue;}
        code[i++]=true;
      }return code;
    }
    static int LongOpen(string text,int start){if(start>=text.Length||text[start]!='[')return -1;int i=start+1;while(i<text.Length&&text[i]=='=')i++;return i<text.Length&&text[i]=='['?i-start-1:-1;}
    static int LongEnd(string text,int start,int equals){int i=start+equals+2;while(i<text.Length){if(text[i]==']'){int end=i+1,n=0;while(end<text.Length&&text[end]=='='){end++;n++;}if(n==equals&&end<text.Length&&text[end]==']')return end+1;}i++;}return text.Length;}
  }
}
