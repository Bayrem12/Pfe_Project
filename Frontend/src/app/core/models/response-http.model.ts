export interface ResponseHttp<T = any> {
  resultat: T;
  status: number;
  fail_Messages: string | null;
}