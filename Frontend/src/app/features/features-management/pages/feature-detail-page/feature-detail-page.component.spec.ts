import { ComponentFixture, TestBed } from '@angular/core/testing';

import { FeatureDetailPageComponent } from './feature-detail-page.component';

describe('FeatureDetailPageComponent', () => {
  let component: FeatureDetailPageComponent;
  let fixture: ComponentFixture<FeatureDetailPageComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FeatureDetailPageComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(FeatureDetailPageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
