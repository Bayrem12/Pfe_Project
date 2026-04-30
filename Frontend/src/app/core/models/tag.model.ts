export interface ProjectTag {
  id: string;
  projectId: string;
  name: string;
  color: string;
  description?: string | null;
}

export interface CreateTagRequest {
  projectId: string;
  name: string;
  color: string;
  description?: string | null;
}
