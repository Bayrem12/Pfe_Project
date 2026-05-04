export interface User {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  role: string;
}

export enum UserRole {
  ADMIN = 'admin',
  USER = 'user',
  GUEST = 'guest'
}
