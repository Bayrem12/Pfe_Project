import { ComponentFixture, TestBed } from '@angular/core/testing';

import { ScenariosListPageComponent } from './scenarios-list-page.component';

describe('ScenariosListPageComponent', () => {
  let component: ScenariosListPageComponent;
  let fixture: ComponentFixture<ScenariosListPageComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ScenariosListPageComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(ScenariosListPageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
