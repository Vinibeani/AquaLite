# Enviar ao seu GitHub

Esta pasta contém somente o projeto publicável. As pastas de investigação, backups e mídias externas da área de trabalho não fazem parte dela.

## Pela interface do GitHub

Crie um repositório vazio no seu perfil e use **Add file → Upload files** para enviar o conteúdo desta pasta. Arquivos que começam com ponto, como `.github` e `.gitignore`, também precisam ser enviados. Para evitar omissões, prefira Git ou GitHub Desktop.

## Com Git

Abra um terminal **nesta pasta**:

```powershell
git init -b main
git add .
git status
git commit -m "Adiciona AquaLite"
```

Depois, crie um repositório vazio no GitHub e execute as instruções de conexão fornecidas por ele. O endereço depende do seu usuário e do nome escolhido para o repositório.

Antes de enviar, confira `git status`: não deve listar `build`, `dist`, dados locais, vídeos ou bibliotecas opcionais. A compilação no GitHub Actions gera o instalador do aplicativo básico, sem os componentes opcionais de sensores e vídeo. O compilador do instalador é baixado da versão oficial fixada e conferido por SHA-256.

Para distribuir uma versão com componentes opcionais, revise também seus avisos e licenças. Não é necessário publicar o conteúdo de `deps` no histórico Git.
