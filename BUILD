Windows:
    Visual Studio is recommended

Ubuntu 14.04:
    1. Install mono version 3.2.8. Unless you're short on space mono-complete is
       easier than figuring out which specific packages you need.
    2. Install certificates needed to download nuget packages. Run:

       curl -fL -o /tmp/certdata.txt https://hg.mozilla.org/releases/mozilla-release/raw-file/5d447d9abfdf/security/nss/lib/ckfw/builtins/certdata.txt 
       mozroots --import --sync --file /tmp/certdata.txt

       Source: https://github.com/travis-ci/travis-build/blob/master/lib/travis/build/script/csharp.rb#L67-L70
    
    3. Download nuget. Run:
    
       wget https://dist.nuget.org/win-x86-commandline/v2.8.6/nuget.exe

    4. Use nuget.exe to install dependencies. Run:
    
       mono nuget.exe restore

    With the prereqs satisfied use "xbuild" to build. There will be plenty of
    warnings related to the older mono stack but typically no errors. Errors that
    occur in the "Tests" project aren't necessarily a problem.

    If Tests builds correctly and you want to run them, refer to .travis.yml for
    the commandline test runner syntax.
