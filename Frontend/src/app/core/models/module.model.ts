export interface Module {
  id: string;
  projectId: string;
  name: string;
  description: string;
  displayOrder: number;
}

export interface CreateModuleRequest {
  projectId: string;
  name: string;
  description: string;
  displayOrder: number;
}