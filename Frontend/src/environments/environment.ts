// Production environment — used by `ng build --configuration production` (Docker).
// All API calls use relative URLs; nginx reverse-proxies them to the right service:
//   /api/ia/*  → http://ia-agent:8000
//   /api/*     → http://api:5288
//   /reports/* → http://ia-agent:8000
export const environment = {
  production: true,
  apiUrl: '/api',   // nginx → http://api:5288/api/...
  iaAgentUrl: '',   // nginx handles /api/ia/ and /reports/ transparently
  googleClientId: '791628194427-al7sf7ttuqougi8ge9nu8rif92qjf1jn.apps.googleusercontent.com',
  githubClientId: 'Ov23lio1dA7ti2vJd9c6'
};