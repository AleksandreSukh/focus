param([string]$version="1.0.19") 
#build using .net
dotnet publish Systems.Sanity.Focus.csproj -c Release --self-contained -r win-x64 -o .\publish

#create package
vpk pack -u Focus -v $version -p .\publish -e Systems.Sanity.Focus.exe

#publish to GitHub
# vpk upload github --publish --repoUrl https://github.com/AleksandreSukh/focus --token $Env:focusGithubToken