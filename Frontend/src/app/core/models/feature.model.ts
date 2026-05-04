// frontend/src/app/features/features/models/feature.model.ts

import { ResponseHttp } from './response-http.model';

export interface FeatureDTO {
  id: string;
  moduleId: string;
  moduleName: string;
  name: string;
  description: string;
  displayOrder: number;
  scenarioCount: number;
  createdDate: Date;
  modifiedDate?: Date;
}

export interface FeatureListDTO {
  id: string;
  moduleId: string;
  name: string;
  description: string;
  displayOrder: number;      
  createdDate: string;
  scenarioCount: number;
}

export interface CreateFeatureRequest {
  moduleId: string;
  name: string;
  description: string;
  displayOrder: number;
}

export interface UpdateFeatureRequest {
  name: string;
  description: string;
  displayOrder: number;
}
