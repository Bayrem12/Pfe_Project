export interface Project {
  id: string;
  name: string;
  description: string;
  url: string;
  isActive: boolean;
  createdDate: Date;
  modifiedDate?: Date;
  members: ProjectMember[];
  modulesCount?: number;
  scenariosCount?: number;
  lastRun?: Date;
  slug?: string;
}

export interface ProjectMember {
  id: string;
  userId: string;
  userName: string;
  firstName: string;
  lastName: string;
  email: string;
  role: string;
  joinedAt: Date;
}

export interface CreateProjectRequest {
  name: string;
  description: string;
  url: string;
  userId: string;
}

export interface UpdateProjectRequest {
  projectId: string;
  name: string;
  description: string;
  url: string;
  isActive: boolean;
}
