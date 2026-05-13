export interface LoginRequest {
  email: string;
  password: string;
}

export interface LoginResult {
  user: {
    id: string;
    email: string;
    firstName: string;
    lastName: string;
    fullName: string;
    isActive: boolean;
    roles: string[];
    createdDate: string;
    modifiedDate: string | null;
  };
  token: string;
  refreshToken: string;
}

export interface RegisterRequest {
  firstName: string;
  lastName: string;
  email: string;
  password: string;
}

export interface RefreshTokenRequest {
  refreshToken: string;
}

export interface ForgotPasswordRequest {
  email: string;
}

export interface ResetPasswordRequest {
  token: string;
  password: string;
  confirmPassword: string;
}

// ✅ Nouveaux types — remplace any
export interface ChangePasswordResponse {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  isActive: boolean;
  fullName: string;
  roles: string[];
  createdDate: string;
  modifiedDate: string | null;
}

export interface ForgotPasswordResponse {
  message: string;
}

export interface ResetPasswordResponse {
  message: string;
}